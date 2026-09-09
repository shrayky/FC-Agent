using CentralServerExchange.Services;
using CSharpFunctionalExtensions;
using Domain.Agent;
using Domain.Agent.Interfaces;
using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Domain.Frontol.Models.Receipts;
using Domain.Messages.Dto;
using Domain.Messages.Enums;
using Domain.Sales.Interfaces;
using DotNetHost;using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CentralServerExchange;

public class SignalRAgentClient
{
    private readonly ILogger<SignalRAgentClient> _logger;
    private readonly IParametersService _parametersService;
    private readonly IApplicationState _applicationState;
    private readonly FrontolSettingsService _frontolSettingsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISalesCursorState _salesCursor;
    
    private string _hubUrl = string.Empty;    private string _agentId = string.Empty;
    
    private HubConnection? _connection;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private bool _isRegistered;

    public SignalRAgentClient(
        ILogger<SignalRAgentClient> logger,
        IParametersService parametersService,
        IApplicationState applicationState,
        FrontolSettingsService frontolSettingsService,
        IServiceScopeFactory serviceScopeFactory,
        ISalesCursorState salesCursor)
    {
        _logger = logger;
        _parametersService = parametersService;
        _applicationState = applicationState;
        _frontolSettingsService = frontolSettingsService;
        _serviceScopeFactory = serviceScopeFactory;
        _salesCursor = salesCursor;
    }    
    public bool ConnectionUp() => !(_connection == null || _connection.State != HubConnectionState.Connected);

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

        _connection.On<string>("AgentRegistered", OnAgentRegistered);
        _connection.On<string>("ReceiveMessage", OnReceiveMessage);
        _connection.On<NewVersionResponse>("NewVersionResponse", OnNewVersionResponse);
        _connection.On<FrontolSettingsRequest>("FrontolSettingsRequest", OnFrontolSettingsRequest);
        _connection.On<FrontolSettingsResponse>("FrontolSettings", OnFrontolSettings);
        _connection.On<PaySystemModeRequest>("PaySystemMode", OnPaySystemMode);
        _connection.On<DeferredReceiptsRequest>("DeferredReceiptsRequest", OnDeferredReceiptsRequest);
        _connection.On<RestartRemoteRequest>("RestartRemote", OnRestartRemote);
        _connection.On<SalesSyncSettingsRequest>("SalesSyncSettings", OnSalesSyncSettings);
        _connection.On<SalesCursorResponse>("SalesCursor", OnSalesCursor);
        _connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "Переподключение к SignalR серверу...");
            _isRegistered = false;
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Переподключено к SignalR серверу. ConnectionId: {ConnectionId}", connectionId);
            _ = Task.Run(async () => await RegisterAgentAsync());
            return Task.CompletedTask;
        };

        _connection.Closed += async error =>
        {
            _logger.LogError(error, "Соединение с SignalR сервером закрыто");
            _isRegistered = false;

            if (error != null)
            {
                await Task.Delay(5000, _cancellationTokenSource.Token);
                await StartAsync();
            }
        };

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
    
    private async Task RegisterAgentAsync()
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно зарегистрировать агента: соединение не установлено");
            return;
        }
       
        var settings = await _parametersService.Current();
        var agentData = new AgentStateResponse()
        {
            AgentToken = _agentId,
            AgentInformation = AgentDataFactory.Current(
                InstalledDotNetRuntimes.ListFromWindows(),
                settings.DatabaseConnection.DatabasePath,
                settings.DatabaseConnection.LogDatabasePath,
                PhysicalDiskHealthReader.List(settings.DatabaseConnection.DatabasePath)),
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
    
    private void OnAgentRegistered(string agentId)
    {
        _isRegistered = true;
        _logger.LogInformation("Агент успешно зарегистрирован на сервере. AgentId: {AgentId}", agentId);
    }
    
    private void OnReceiveMessage(string message)
    {
        _logger.LogInformation("Получено сообщение от сервера: {Message}", message);
    }

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

        const string methodName = "FrontolSettings";
        
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно отправить данные: соединение не установлено");
            return;
        }

        if (!_isRegistered)
        {
            _logger.LogWarning("Агент не зарегистрирован. Попытка повторной регистрации...");
            await RegisterAgentAsync();
            return;
        }

        var answerMessage = new FrontolSettingsResponse()
        {
            AgentToken = _agentId,
            MessageType = MessageType.FrontolSettings,
            Settings = frontolSettings.Value
        };
        
        try
        {
            await _connection.InvokeAsync(methodName, answerMessage, _cancellationTokenSource.Token);
            _logger.LogDebug("Данные агента отправлены на сервер");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке данных агента");
        }
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

    public async Task RequestSalesCursor()
    {
        const string methodName = "SalesCursorRequest";

        if (!CanSend(out _))
            return;

        try
        {
            var message = new SalesCursorRequest
            {
                AgentToken = _agentId
            };

            await _connection!.InvokeAsync(methodName, message, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запроса курсора продаж");
        }
    }

    public async Task<Result> SendSalesDocuments(IReadOnlyList<SalesDocument> documents)
    {
        const string methodName = "SalesDocuments";

        if (documents.Count == 0)
            return Result.Success();

        if (!CanSend(out var error))
            return Result.Failure(error);

        try
        {
            var message = new SalesDocumentsMessage
            {
                AgentToken = _agentId,
                Documents = documents.ToList()
            };

            await _connection!.InvokeAsync(methodName, message, _cancellationTokenSource.Token);
            _logger.LogDebug("Отправлено чеков продаж: {Count}", documents.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки чеков продаж");
            return Result.Failure(ex.Message);
        }
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

    private static Result ToUnit<T>(Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    
    public async Task StopAsync()
    {
        await _cancellationTokenSource.CancelAsync();

        if (_connection == null)
            return;
        
        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _logger.LogInformation("Клиент SignalR остановлен");
    }
    
    public async Task SendAgentState(AgentStateResponse agentStateResponse)
    {
        const string methodName = "AgentStateMessage";
        
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно отправить данные: соединение не установлено");
            return;
        }

        if (!_isRegistered)
        {
            _logger.LogWarning("Агент не зарегистрирован. Попытка повторной регистрации...");
            await RegisterAgentAsync();
            return;
        }

        try
        {
            agentStateResponse.AgentToken = _agentId;

            await _connection.InvokeAsync(methodName, agentStateResponse, _cancellationTokenSource.Token);
            _logger.LogDebug("Данные агента отправлены на сервер");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке данных агента");
        }
    }
    
    public async Task AskNewVersion()
    {
        const string methodName = "NewVersionRequestMessage";
        
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно отправить данные: соединение не установлено");
            return;
        }

        var settings = await _parametersService.Current();
            
        if (!settings.CentralServerSettings.DownloadNewVersion)
            return;

        if (!_isRegistered)
        {
            _logger.LogWarning("Агент не зарегистрирован. Попытка повторной регистрации...");
            await RegisterAgentAsync();
            return;
        }

        NewVersionRequest message = new()
        {
            AgentToken = _agentId,
            AgentInformation = AgentDataFactory.Current(
                InstalledDotNetRuntimes.ListFromWindows(),
                settings.DatabaseConnection.DatabasePath,
                settings.DatabaseConnection.LogDatabasePath,
                PhysicalDiskHealthReader.List(settings.DatabaseConnection.DatabasePath))
        };
        
        try
        {
            await _connection.InvokeAsync(methodName, message, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке данных агента");
        }
    }

    public async Task<Result> SendFrontolLogs(List<LogRecord> logs)
    {
        const string methodName = "FrontolLogMessage";

        if (logs.Count == 0)
        {
            const string err = "Нет логов для отправки.";
            _logger.LogDebug(err);
            return Result.Failure(err);
        }    
        
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            const string err = "Невозможно отправить данные: соединение не установлено";
            _logger.LogWarning(err);
            return Result.Failure(err);
        }

        if (!_isRegistered)
        {
            const string err = "Агент не зарегистрирован. Попытка повторной регистрации...";
            _logger.LogWarning(err);
            await RegisterAgentAsync();
            
            return  Result.Failure(err);
        }

        try
        {
            FrontolLogsMessage message = new()
            {
                AgentToken = _agentId,
                Logs = logs
            };

            await _connection.InvokeAsync(methodName, message, _cancellationTokenSource.Token);
            _logger.LogDebug("Данные логов отправлены на сервер");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке логов");
        }
        
        return Result.Success();
    }

    public async Task<Result> SendFrontolSettingsApplyingIsSuccess()
    {
        const string methodName = "FrontolSettingsApplying";

        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            const string err = "Невозможно отправить данные: соединение не установлено";
            _logger.LogWarning(err);
            return Result.Failure(err);
        }

        if (!_isRegistered)
        {
            const string err = "Агент не зарегистрирован. Попытка повторной регистрации...";
            _logger.LogWarning(err);
            await RegisterAgentAsync();

            return Result.Failure(err);
        }

        try
        {
            FrontolSettingsApplyingState message = new()
            {
                AgentToken = _agentId,
                Success = true
            };

            await _connection.InvokeAsync(methodName, message, _cancellationTokenSource.Token);
            _logger.LogDebug("Данные логов отправлены на сервер");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке логов");
        }

        return Result.Success();
    }

    private async Task SendDeferredReceipts(DeferredReceiptOperation operation, Result<ReceiptList> result)
    {
        const string methodName = "DeferredReceipts";

        if (!CanSend(out _))
            return;

        try
        {
            var message = new DeferredReceiptsResponse
            {
                AgentToken = _agentId,
                Operation = operation,
                Success = result.IsSuccess,
                Error = result.IsFailure ? result.Error : string.Empty,
                Receipts = result.IsSuccess ? result.Value.Receipts : [],
                PaymentKinds = result.IsSuccess ? result.Value.PaymentKinds : [],
                PrintGroups = result.IsSuccess ? result.Value.PrintGroups : []
            };

            await _connection!.InvokeAsync(methodName, message, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки отложенных чеков");
        }
    }

    private bool CanSend(out string error)
    {
        error = string.Empty;

        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            error = "Невозможно отправить данные: соединение не установлено";
            _logger.LogWarning(error);
            return false;
        }

        if (_isRegistered)
            return true;

        error = "Агент не зарегистрирован";
        _logger.LogWarning(error);
        return false;
    }
}