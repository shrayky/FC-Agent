# FC-Agent: что нужно знать до правок

Файл читается агентом в начале сессии, поэтому здесь только то, чего не видно из кода.

## Слои

- `src/core`: `Domain` (модели, интерфейсы, константы — мелкие файлы, один тип на файл) →
  `Application` (сервисы, регистрируются атрибутом `[AutoRegisterService(...)]`) → `Shared`
  (общие помощники: DI-атрибуты, `FilesFolders`, JSON, HTTP).
- `src/infrastructure`: адаптеры — `CentralServerExchange` (обмен с `fc` по SignalR),
  `FrontolDatabase`, `Configuration`, `Logger`, `DotNetHost`.
- `src/presentation`: `HostApp` — host `fc` (AOT), `ViewApp` — продукт `fc-agent` (`Sdk.Web`).

Обмен с `fc` идёт существующими сообщениями SignalR: новые `MessageType` и методы хаба не заводятся,
а контракт в `Domain.Frontol.Models.Settings` меняется только вместе с `fc`.

## Каталоги в ProgramData

Единственный источник путей — `Shared.FilesFolders.Folders`, каталог данных агента:

```csharp
Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name)
// %ProgramData%\Automation\fc\<подкаталог>
```

Хардкодить `C:\ProgramData\...` нельзя. `fc-agent` — имя продукта (`ViewApp`), а каталог данных
агента называется `fc`, поэтому `...\fc-agent\...` в путях — ошибка.

## Каталог библиотек fScript

Библиотеки приходят из `fc` сверху вниз и целиком заменяют файлы подкаталога `fScript`.
Пустой список очищает файлы; подкаталоги не удаляются.

Замена не начинается, пока набор не проверен и не записан целиком во временный каталог
`fScript.staging` рядом. Недопустимое или зарезервированное Windows имя (`CON`, `PRN`, `AUX`,
`NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`, в том числе с расширением), пустое имя, повтор имени
или текст `null` — каталог не меняется. Лишние файлы удаляются только после переноса
уже записанного набора.

## Сборка и тесты

```powershell
dotnet build FrontolConfigurator-Agent.sln -c Debug -m:1 /nodeReuse:false
dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --no-restore
```

`-m:1 /nodeReuse:false` — сборка одним узлом без переиспользования: в песочнице агента
MSBuild-узлы и VSTest-хост иначе не стартуют (сборка падает с «Ошибок: 0», тесты — с
`Win32Exception (5)`).
