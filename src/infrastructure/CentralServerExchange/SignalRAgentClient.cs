using CentralServerExchange.Services;
using CSharpFunctionalExtensions;
using Domain.Agent.Dto;
using Domain.AppState.Interfaces;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Dto;
using Domain.Messages.Dto;
using Domain.Messages.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange;

public class SignalRAgentClient
{
    private readonly ILogger<SignalRAgentClient> _logger;
    private readonly IParametersService _parametersService;
    private readonly IApplicationState _applicationState;
    private readonly FrontolSettingsService _frontolSettingsService;
    
    private string _hubUrl = string.Empty;
    private string _agentId = string.Empty;
    
    private HubConnection? _connection;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private bool _isRegistered;

    public SignalRAgentClient(ILogger<SignalRAgentClient> logger, IParametersService parametersService, IApplicationState applicationState, FrontolSettingsService frontolSettingsService)
    {
        _logger = logger;
        _parametersService = parametersService;
        _applicationState = applicationState;
        _frontolSettingsService = frontolSettingsService;
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
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect([
                TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
            ])
            .Build();

        _connection.On<string>("AgentRegistered", OnAgentRegistered);
        _connection.On<string>("ReceiveMessage", OnReceiveMessage);
        _connection.On<NewVersionResponse>("NewVersionResponse", OnNewVersionResponse);
        _connection.On<FrontolSettingsRequest>("FrontolSettingsRequest", OnFrontolSettingsRequest);
        _connection.On<FrontolSettingsResponse>("FrontolSettings", OnFrontolSettings);

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
            _logger.LogError(ex, "Ошибка при подключении к SignalR серверу");
        }
    }
    
    private async Task RegisterAgentAsync()
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Невозможно зарегистрировать агента: соединение не установлено");
            return;
        }
       
        var agentData = new AgentStateResponse()
        {
            AgentToken = _agentId,
            AgentInformation = new AgentData
            {
                Version = ApplicationInformation.Version,
                Assembly = ApplicationInformation.Assembly,    
            },
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

        await _frontolSettingsService.ApplySettings(message.Settings);
    }
    
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
            AgentInformation = new AgentData
            {
                Version = ApplicationInformation.Version,
                Assembly = ApplicationInformation.Assembly,
            }
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
}