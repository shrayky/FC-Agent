using Domain.Angent.Dto;
using Domain.Configuration.Constants;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Dto;
using Domain.Messages.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace CentralServerExchange;

public class SignalRAgentClient
{
    private readonly ILogger<SignalRAgentClient> _logger;
    private readonly IParametersService _parametersService;
    
    private string _hubUrl = string.Empty;
    private string _agentId = string.Empty;
    
    private HubConnection? _connection;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private bool _isRegistered;
    private bool _isStarted;

    public SignalRAgentClient(ILogger<SignalRAgentClient> logger, IParametersService parametersService)
    {
        _logger = logger;
        _parametersService = parametersService;
    }
    
    public bool ConnectionUp() => _isStarted;

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
            _isStarted = true;

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
       
        var agentData = new Message()
        {
            AgentToken = _agentId,
            AgentInformation = new AgentData
            {
                Version = ApplicationInformation.Version,
                Assembly = ApplicationInformation.Assembly,    
            },
            FrontolInformation = new FrontolInformation()
        };
        
        try
        {
            await _connection.InvokeAsync("RegisterAgent", agentData, _cancellationTokenSource.Token);
            _logger.LogInformation("Запрос на регистрацию агента отправлен. AgentId: {AgentId}", _agentId);
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
        // TODO: Обработка сообщения
    }
    
    public async Task StopAsync()
    {
        await _cancellationTokenSource.CancelAsync();

        if (_connection == null)
            return;
        
        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _isStarted = false;
        _logger.LogInformation("Клиент SignalR остановлен");
    }
    
    public async Task SendAgentDataAsync(Message message)
    {
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
            message.AgentToken = _agentId;

            await _connection.InvokeAsync("SendAgentData", message, _cancellationTokenSource.Token);
            _logger.LogDebug("Данные агента отправлены на сервер");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке данных агента");
        }
    }
}