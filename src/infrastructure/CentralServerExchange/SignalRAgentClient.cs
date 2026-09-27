using CentralServerExchange.Services;
using CSharpFunctionalExtensions;
using Domain.Agent;
using Domain.Agent.Dto;
using Domain.Agent.Interfaces;
using Domain.AppState.Interfaces;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Domain.Frontol.Models.Receipts;
using Domain.Messages.Dto;
using Domain.Messages.Enums;
using Domain.Sales.Interfaces;
using DotNetHost;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange;

/// <summary>
/// SignalR-клиент агента: подключение к центральному серверу, приём команд и отправка ответов.
/// </summary>
/// <remarks>
/// Имена hub-методов задаются одним аргументом в вызове <c>Send(Message, "HubMethod")</c> —
/// держать имя метода рядом с местом отправки важнее, чем спрятать его в реестр:
/// так видно, какой метод сервера дёргается и какое сообщение при этом уходит.
/// </remarks>
public class SignalRAgentClient
{
    private const string NoConnectionError = "Невозможно отправить данные: соединение не установлено";
    private const string NotRegisteredError = "Агент не зарегистрирован";

    private readonly ILogger<SignalRAgentClient> _logger;
    private readonly IParametersService _parametersService;
    private readonly IApplicationState _applicationState;
    private readonly FrontolSettingsService _frontolSettingsService;
    private readonly AgentLogsService _agentLogsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISalesCursorState _salesCursor;

    private string _hubUrl = string.Empty;
    private string _agentId = string.Empty;

    private HubConnection? _connection;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _isRegistered;

    public SignalRAgentClient(
        ILogger<SignalRAgentClient> logger,
        IParametersService parametersService,
        IApplicationState applicationState,
        FrontolSettingsService frontolSettingsService,
        AgentLogsService agentLogsService,
        IServiceScopeFactory serviceScopeFactory,
        ISalesCursorState salesCursor)
    {
        _logger = logger;
        _parametersService = parametersService;
        _applicationState = applicationState;
        _frontolSettingsService = frontolSettingsService;
        _agentLogsService = agentLogsService;
        _serviceScopeFactory = serviceScopeFactory;
        _salesCursor = salesCursor;
    }

    public bool ConnectionUp() => _connection is { State: HubConnectionState.Connected };

    public async Task StartAsync()
    {
        var settings = await _parametersService.Current();
        _hubUrl = $"{settings.CentralServerSettings.Address}/hubs/exchange";
        _agentId = settings.CentralServerSettings.Token;

        if (_hubUrl == string.Empty || _agentId == string.Empty)
        {
            _logger.LogDebug("Нет настроек для подключения к центральному серверу");
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                if (ForceHttp11MessageHandler.IsRequiredOnThisOs)
                    options.HttpMessageHandlerFactory = inner => new ForceHttp11MessageHandler(inner);
            })
            .WithAutomaticReconnect([
                TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
            ])
            .Build();

        SubscribeHandlers(_connection);

        try
        {
            await _connection.StartAsync(_cancellationTokenSource.Token);
            _logger.LogInformation("Подключено к SignalR серверу: {HubUrl}", _hubUrl);

            await RegisterAgentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при подключении к SignalR серверу. HubUrl: {HubUrl}, OS: {OS}",
                _hubUrl,
                Environment.OSVersion);
        }
    }

    /// <summary>
    /// Подписки на команды сервера. Одна строка на команду: имя hub-метода, тип запроса, обработчик.
    /// </summary>
    private void SubscribeHandlers(HubConnection connection)
    {
        connection.On<string>("AgentRegistered", OnAgentRegistered);
        connection.On<string>("ReceiveMessage", OnReceiveMessage);
        connection.On<NewVersionResponse>("NewVersionResponse", OnNewVersionResponse);
        connection.On<FrontolSettingsRequest>("FrontolSettingsRequest", OnFrontolSettingsRequest);
        connection.On<FrontolSettingsResponse>("FrontolSettings", OnFrontolSettings);
        connection.On<PaySystemModeRequest>("PaySystemMode", OnPaySystemMode);
        connection.On<DeferredReceiptsRequest>("DeferredReceiptsRequest", OnDeferredReceiptsRequest);
        connection.On<LicenseActivationRequest>("LicenseActivationRequest", OnLicenseActivationRequest);
        connection.On<RestartRemoteRequest>("RestartRemote", OnRestartRemote);
        connection.On<SalesSyncSettingsRequest>("SalesSyncSettings", OnSalesSyncSettings);
        connection.On<SalesCursorResponse>("SalesCursor", OnSalesCursor);
        connection.On<SalesDictionaryBatchMessage>("SalesDictionary", OnSalesDictionary);
        connection.On<AgentLogsRequest>("AgentLogsRequest", OnAgentLogsRequest);

        connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "Переподключение к SignalR серверу...");
            _isRegistered = false;
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Переподключено к SignalR серверу. ConnectionId: {ConnectionId}", connectionId);
            _ = Task.Run(RegisterAgentAsync);
            return Task.CompletedTask;
        };

        connection.Closed += async error =>
        {
            _logger.LogError(error, "Соединение с SignalR сервером закрыто");
            _isRegistered = false;

            if (error != null)
            {
                await Task.Delay(5000, _cancellationTokenSource.Token);
                await StartAsync();
            }
        };
    }

    private AgentData BuildAgentData(Parameters settings) =>
        AgentDataFactory.Current(
            InstalledDotNetRuntimes.ListFromWindows(),
            settings.DatabaseConnection.DatabasePath,
            settings.DatabaseConnection.LogDatabasePath,
            PhysicalDiskHealthReader.List(settings.DatabaseConnection.DatabasePath),
            DriverAto10lLogsSizeReader.TotalBytes(message => _logger.LogWarning(message)));

    private async Task RegisterAgentAsync()
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно зарегистрировать агента: соединение не установлено");
            return;
        }

        var settings = await _parametersService.Current();
        var agentData = new AgentStateResponse
        {
            AgentToken = _agentId,
            AgentInformation = BuildAgentData(settings),
        };

        try
        {
            await _connection.InvokeAsync("RegisterAgent", agentData, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации агента");
        }
    }

    // ---------------------------------------------------------------------
    // Отправка на сервер
    // ---------------------------------------------------------------------

    /// <summary>
    /// Отправляет сообщение и заполняет <see cref="Message.AgentToken"/>.
    /// </summary>
    private async Task<Result> Send(Message message, string hubMethod)
    {
        message.AgentToken = _agentId;

        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("{Error}. HubMethod: {HubMethod}", NoConnectionError, hubMethod);
            return Result.Failure(NoConnectionError);
        }

        if (!_isRegistered)
        {
            _logger.LogWarning("{Error}. Попытка повторной регистрации... HubMethod: {HubMethod}", NotRegisteredError, hubMethod);
            await RegisterAgentAsync();
            return Result.Failure(NotRegisteredError);
        }

        try
        {
            await _connection.InvokeAsync(hubMethod, message, _cancellationTokenSource.Token);
            _logger.LogDebug("Сообщение {MessageType} отправлено на сервер ({HubMethod})", message.MessageType, hubMethod);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки сообщения {MessageType} ({HubMethod})", message.MessageType, hubMethod);
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Отправка ответа на команду сервера: нет соединения или агент не зарегистрирован — просто выходим.
    /// </summary>
    private async Task<Result> TrySend(Message message, string hubMethod)
    {
        if (!CanSend(out var error))
            return Result.Failure(error);

        return await Send(message, hubMethod);
    }

    /// <summary>
    /// В отличие от <see cref="TrySend"/> пытается перерегистрироваться: вызывающий код инициирует
    /// отправку сам (по таймеру), поэтому не должен молча терять сообщение.
    /// </summary>
    private async Task<Result> SendWithReregistration(Message message, string hubMethod)
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("{Error}. HubMethod: {HubMethod}", NoConnectionError, hubMethod);
            return Result.Failure(NoConnectionError);
        }

        return await Send(message, hubMethod);
    }

    /// <summary>
    /// Общая проверка готовности соединения. Возвращает <c>false</c>, если отправлять нельзя.
    /// </summary>
    private bool CanSend(out string error)
    {
        error = string.Empty;

        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            error = NoConnectionError;
            _logger.LogWarning(error);
            return false;
        }

        if (_isRegistered)
            return true;

        error = NotRegisteredError;
        _logger.LogWarning(error);
        return false;
    }

    public async Task<Result> SendAgentState(AgentStateResponse agentState)
    {
        var result = await SendWithReregistration(agentState, "AgentStateMessage");
        if (result.IsFailure)
            return result;

        _logger.LogDebug("Данные агента отправлены на сервер");
        return Result.Success();
    }

    public async Task AskNewVersion()
    {
        var settings = await _parametersService.Current();

        if (!settings.CentralServerSettings.DownloadNewVersion)
            return;

        await SendWithReregistration(
            new NewVersionRequest { AgentInformation = BuildAgentData(settings) },
            "NewVersionRequestMessage");
    }

    public async Task<Result> SendFrontolLogs(List<LogRecord> logs)
    {
        if (logs.Count == 0)
        {
            const string error = "Нет логов для отправки.";
            _logger.LogDebug(error);
            return Result.Failure(error);
        }

        return await SendWithReregistration(new FrontolLogsMessage { Logs = logs }, "FrontolLogMessage");
    }

    private async Task SendAgentLogs(AgentLogsResponse logs) =>
        await TrySend(logs, "AgentLogs");

    public async Task<Result> SendFrontolSettingsApplyingIsSuccess() =>
        await TrySend(new FrontolSettingsApplyingState { Success = true }, "FrontolSettingsApplying");

    public async Task RequestSalesCursor() =>
        await TrySend(new SalesCursorRequest(), "SalesCursorRequest");

    public async Task<Result> SendSalesDocuments(IReadOnlyList<SalesDocument> documents)
    {
        if (documents.Count == 0)
            return Result.Success();

        var result = await TrySend(
            new SalesDocumentsMessage { Documents = documents.ToList() },
            "SalesDocuments");

        if (result.IsSuccess)
            _logger.LogDebug("Отправлено чеков продаж: {Count}", documents.Count);

        return result;
    }

    private async Task SendSalesDictionaryApplying(Result result) =>
        await TrySend(
            new SalesDictionaryApplyingState
            {
                Success = result.IsSuccess,
                Message = result.IsFailure ? result.Error : string.Empty
            },
            "SalesDictionaryApplying");

    private async Task SendDeferredReceipts(DeferredReceiptOperation operation, Result<ReceiptList> result) =>
        await TrySend(
            new DeferredReceiptsResponse
            {
                Operation = operation,
                Success = result.IsSuccess,
                Error = result.IsFailure ? result.Error : string.Empty,
                Receipts = result.IsSuccess ? result.Value.Receipts : [],
                PaymentKinds = result.IsSuccess ? result.Value.PaymentKinds : [],
                PrintGroups = result.IsSuccess ? result.Value.PrintGroups : []
            },
            "DeferredReceipts");

    private async Task SendLicenseActivationResult(string licenseId, Result result) =>
        await TrySend(
            new LicenseActivationResponse
            {
                LicenseId = licenseId,
                Success = result.IsSuccess,
                Error = result.IsFailure ? result.Error : string.Empty
            },
            "LicenseActivation");

    public async Task StopAsync()
    {
        await _cancellationTokenSource.CancelAsync();

        if (_connection == null)
            return;

        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _logger.LogInformation("Клиент SignalR остановлен");
    }

    // ---------------------------------------------------------------------
    // Обработка команд сервера
    // ---------------------------------------------------------------------

    private void OnAgentRegistered(string agentId)
    {
        _isRegistered = true;
        _logger.LogInformation("Агент успешно зарегистрирован на сервере. AgentId: {AgentId}", agentId);
    }

    private void OnReceiveMessage(string message) =>
        _logger.LogInformation("Получено сообщение от сервера: {Message}", message);

    private void OnNewVersionResponse(NewVersionResponse message)
    {
        _logger.LogInformation("Получена информация о обновлении от сервера: {message}", message);
        _applicationState.NewVersionInformationUpdate(message);
    }

    private async Task OnFrontolSettingsRequest(FrontolSettingsRequest message)
    {
        _logger.LogInformation("Получен запрос настроек фронтола от сервера: {message}", message);

        var frontolSettings = await _frontolSettingsService.ReadFrontolSettings();

        if (frontolSettings.IsFailure)
        {
            _logger.LogError(frontolSettings.Error);
            return;
        }

        await TrySend(
            new FrontolSettingsResponse { Settings = frontolSettings.Value },
            "FrontolSettings");
    }

    private async Task OnFrontolSettings(FrontolSettingsResponse message)
    {
        _logger.LogInformation("Получен пакет с настройками фронтола от сервера: {message}", message);

        var applyingResult = await _frontolSettingsService.ApplySettings(message.Settings);

        if (applyingResult.IsSuccess)
            await SendFrontolSettingsApplyingIsSuccess();
    }

    private async Task OnPaySystemMode(PaySystemModeRequest message)
    {
        _logger.LogInformation("Получена команда режима банковских систем: {Mode}", message.Mode);

        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPaySystemModeService>();
        var result = await service.ChangeMode(message.Mode);

        if (result.IsFailure)
            _logger.LogError(result.Error);
    }

    private Task OnRestartRemote(RestartRemoteRequest message)
    {
        try
        {
            _logger.LogWarning("Получена команда рестарта fc-remote");
            using var scope = _serviceScopeFactory.CreateScope();
            var restarter = scope.ServiceProvider.GetRequiredService<IFcRemoteRestarter>();
            var result = restarter.Restart();
            if (result.IsFailure)
                _logger.LogError(result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка рестарта fc-remote");
        }

        return Task.CompletedTask;
    }

    private async Task OnSalesSyncSettings(SalesSyncSettingsRequest message)
    {
        try
        {
            var current = await _parametersService.Current();
            current.SalesSettings ??= new SalesSettings();
            current.SalesSettings.LoadSales = message.LoadSales;
            current.SalesSettings.PollIntervalSeconds =
                message.PollIntervalSeconds <= 0 ? 60 : message.PollIntervalSeconds;

            await _parametersService.Update(current);
            _logger.LogInformation(
                "Настройки сбора продаж: load={Load}, interval={Interval}",
                current.SalesSettings.LoadSales,
                current.SalesSettings.PollIntervalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения настроек сбора продаж");
        }
    }

    private void OnSalesCursor(SalesCursorResponse message)
    {
        _salesCursor.Set(message.DocumentNumber);
        _logger.LogInformation("Курсор продаж: {Number}", message.DocumentNumber);
    }

    private async Task OnSalesDictionary(SalesDictionaryBatchMessage message)
    {
        _logger.LogInformation(
            "Получен справочник продаж: групп {groups}, товаров {wares}",
            message.Groups.Count,
            message.Wares.Count);

        using var scope = _serviceScopeFactory.CreateScope();
        var apply = scope.ServiceProvider.GetRequiredService<IWareApplyService>();
        var result = await apply.Apply(message.Groups, message.Wares);
        if (result.IsFailure)
            _logger.LogError(result.Error);

        await SendSalesDictionaryApplying(result);
    }

    private async Task OnAgentLogsRequest(AgentLogsRequest message)
    {
        _logger.LogInformation("Получен запрос логов агента: {File}", message.SelectedLogFileName);

        AgentLogsResponse response;

        try
        {
            var logs = await _agentLogsService.Collect(message.SelectedLogFileName);

            response = logs.IsSuccess
                ? logs.Value
                : new AgentLogsResponse { Success = false, Error = logs.Error };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения логов агента");
            response = new AgentLogsResponse { Success = false, Error = ex.Message };
        }

        await SendAgentLogs(response);
    }

    private async Task OnDeferredReceiptsRequest(DeferredReceiptsRequest message)
    {
        _logger.LogInformation("Отложенные чеки: {Operation}", message.Operation);

        using var scope = _serviceScopeFactory.CreateScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IFrontolDeferredReceipts>();

        try
        {
            var command = await ExecuteDeferredOperation(receipts, message);
            if (command.IsFailure)
            {
                _logger.LogError(command.Error);
                await SendDeferredReceipts(message.Operation, Result.Failure<ReceiptList>(command.Error));
                return;
            }

            await SendDeferredReceipts(message.Operation, await receipts.List());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка команды отложенного чека");
            await SendDeferredReceipts(message.Operation, Result.Failure<ReceiptList>(ex.Message));
        }
    }

    private static async Task<Result> ExecuteDeferredOperation(
        IFrontolDeferredReceipts receipts,
        DeferredReceiptsRequest message)
    {
        if (message.Operation == DeferredReceiptOperation.List)
            return Result.Success();

        if (message.Operation == DeferredReceiptOperation.Cancel)
            return ToUnit(await receipts.Cancel(message.DocumentId));

        if (message.Operation == DeferredReceiptOperation.Close)
            return ToUnit(await receipts.Close(message.DocumentId, message.Payments));

        if (message.Operation == DeferredReceiptOperation.AddPayment)
            return ToUnit(await receipts.AddPayment(message.DocumentId, message.Payments));

        return Result.Failure("Неизвестная операция с отложенным чеком");
    }

    private async Task OnLicenseActivationRequest(LicenseActivationRequest message)
    {
        _logger.LogInformation("Получена команда активации лицензии {LicenseId}", message.LicenseId);

        using var scope = _serviceScopeFactory.CreateScope();
        var activator = scope.ServiceProvider.GetRequiredService<IAtolLicenseActivator>();

        Result result;

        try
        {
            result = await activator.Activate(message.LicenseId, message.ShopName, message.Company);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка активации лицензии {LicenseId}", message.LicenseId);
            result = Result.Failure(ex.Message);
        }

        await SendLicenseActivationResult(message.LicenseId, result);
    }

    private static Result ToUnit<T>(Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
}
