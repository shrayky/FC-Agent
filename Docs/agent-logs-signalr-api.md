# Команда получения логов агента по SignalR

Описание контракта для реализации серверной части в репозитории `fc`.

Агент отдаёт по запросу **свои собственные логи** — файлы `fc*.log` из каталога
`%ProgramData%\Automation\fc\log` (Serilog, суточная прокрутка, `logDepth` файлов).
Логи Frontol из БД кассы к этой команде отношения не имеют: они уходят на сервер
периодическим пушем `FrontolLogMessage`.

Сторона агента уже реализована:

| Что | Где |
| --- | --- |
| DTO запроса | `src/core/Domain/Messages/Dto/AgentLogsRequest.cs` |
| DTO ответа | `src/core/Domain/Messages/Dto/AgentLogsResponse.cs` |
| Тип сообщения | `MessageType.AgentLogs` |
| Сборка пакета | `src/infrastructure/CentralServerExchange/Services/AgentLogsService.cs` |
| Подписка и обработчик | `SignalRAgentClient.SubscribeHandlers` → `OnAgentLogsRequest` |

## Транспорт

Hub `ExchangeHub` по адресу `{CentralServerSettings.Address}/hubs/exchange`.
Сервер шлёт команду конкретному соединению агента — `Clients.Client(connectionId)`,
где `connectionId` берётся из кэша `signalr_connection_{token}` (как в
`RequestFrontolConfiguration`).

JSON в примерах — схематичный: на стороне `fc` достаточно повторить имена свойств
C#-DTO (SignalR JSON-протокол сам приводит их к camelCase); критичны имена свойств и
тип сообщения.

## 1. Сервер → агент: запрос логов

Hub-метод: **`AgentLogsRequest`**

```json
{
  "agentToken": "token-agent",
  "messageType": 14,
  "selectedLogFileName": ""
}
```

`selectedLogFileName`:

| Значение | Поведение агента |
| --- | --- |
| `""` | отдаёт только список файлов, текст не читается |
| `"now"` | отдаёт самый свежий файл каталога |
| `"20260926"` | отдаёт файл с этим суффиксом даты (для `fc20260926.log`) |

Суффикс — имя файла без префикса `fc` и без расширения. Ровно те же значения
приходят обратно в `logFilesNames`, поэтому список из ответа можно сразу слать в
запрос повторно.

## 2. Агент → сервер: пакет логов

Hub-метод: **`AgentLogs`**

```json
{
  "agentToken": "token-agent",
  "messageType": 14,
  "logFilesNames": ["20260926", "20260925"],
  "selectedLogFileName": "20260926",
  "text": "2026-09-26 09:12:44.123 +07:00 [INF] ...",
  "success": true,
  "error": ""
}
```

| Поле | Смысл |
| --- | --- |
| `logFilesNames` | суффиксы дат всех файлов каталога — для выпадающего списка в UI |
| `selectedLogFileName` | что реально прочитано: `""` или `"now"` из запроса заменяется на конкретный суффикс |
| `text` | содержимое файла целиком; `""`, если файл не выбран или не прочитан |
| `success` / `error` | `false` + причина, если отдать нечего |

Причины отказа (`success: false`):

- `Файлы логов агента не найдены` — каталога нет, файлов нет или файловое
  логирование выключено в настройках агента;
- `Лог 20260926 не найден` — запрошен суффикс, которого нет в каталоге;
- текст исключения — файл занят/недоступен.

Порядок `logFilesNames` не гарантирован (как отдаёт файловая система) — если в UI
нужен список «свежие сверху», сортируйте суффиксы на стороне `fc` по убыванию.

Ответ приходит только если агент подключён и зарегистрирован: иначе агент пишет
warning в свой лог и молчит (так же ведут себя ответы на остальные команды), поэтому
на стороне `fc` нужен таймаут ожидания.

## Что нужно добавить в `fc`

1. `src/core/Domain/Messages/Enums/MessageType.cs` — `AgentLogs` **в конец** списка,
   после `LicenseActivation`. Enum уходит по проводу числом (`14`), поэтому порядок
   обязан совпадать с агентом.
2. `src/core/Domain/Messages/Dto/` — `AgentLogsRequest` и `AgentLogsResponse` c
   `: IMessage`, поля как в контракте выше, `MessageType = MessageType.AgentLogs`.
3. `src/infrastructure/SignalR/Hubs/ExchangeHub.cs` — приём ответа:

   ```csharp
   public async Task AgentLogs(AgentLogsResponse message)
   {
       await ProcessMessage(message);
   }
   ```

4. `MessageHandlerFabric` + обработчик в новой папке `MessageHandlers/AgentLogsWorkshop`:
   ветка `MessageType.AgentLogs => _agentLogsMessageHandler`. Без ветки ответ попадёт
   в `_ => _agentStateMessageHandler` и будет разобран как состояние агента.
5. `IMessagesToAgentService` / `OutComingMessageService` — отправка команды:

   ```csharp
   public async Task<Result> SendAgentLogsRequest(string token, string selectedLogFileName)
   {
       const string methodName = "AgentLogsRequest";
       // лицензия + connectionId из signalr_connection_{token} — как в RequestFrontolConfiguration
       var message = new AgentLogsRequest
       {
           AgentToken = token,
           SelectedLogFileName = selectedLogFileName
       };
       await _hubContext.Clients.Client(connectionId).SendAsync(methodName, message);
       return Result.Success();
   }
   ```

6. HTTP/UI для оператора — по образцу `LogsController` (`api/logs/{logFileName}`),
   но с походом в агент через SignalR и ожиданием ответа. Для списка файлов
   достаточно запроса с пустым `selectedLogFileName`.

## Ручная проверка

1. Касса с запущенным агентом; в его логе после запроса появляется строка
   `Получен запрос логов агента: now`.
2. Запрос с `""` → в ответе список файлов и пустой `text`.
3. Запрос с `"now"` → `text` непустой, `selectedLogFileName` — суффикс свежего файла.
4. Запрос с несуществующим суффиксом → `success: false`, `error: Лог … не найден`.
5. Выключить файловое логирование в настройках агента и перезапустить → запрос
   возвращает `success: false`, `error: Файлы логов агента не найдены`.
