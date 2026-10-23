# Настройки документов Frontol в пакете настроек

Контракт для `fc`. Новых hub-методов нет: списки едут внутри уже существующего `FrontolSettings`.

Агент читает и пишет таблицы `DOCKIND`, `DOCTEMPLATE`, `DOCKINDTMPL`, `PRINTGROUP`, `COMMENT`. В пакете нет `id` базы и нет полей внутренней синхронизации Frontol (`bdoCode`, `chng`, `ownerBdo`, `deleted`, `taxGroupId`). Связи между справочниками — коды.

| Что | Где |
| --- | --- |
| Списки | `FrontolSettings` — `src/core/Domain/Frontol/Models/Settings/` |
| Чтение и запись | `DocumentConfigurationRepository` |
| Вложение в пакет | `FrontolSettingsService.ReadFrontolSettings` / `ApplySettings` |

## Транспорт

Hub `ExchangeHub`, адрес `{CentralServerSettings.Address}/hubs/exchange`.

| Направление | Hub-метод | DTO |
| --- | --- | --- |
| Сервер → агент, отдать настройки кассы | `FrontolSettingsRequest` | `FrontolSettingsRequest` |
| Агент → сервер, снимок настроек | `FrontolSettings` | `FrontolSettingsResponse` |
| Сервер → агент, записать настройки | `FrontolSettings` | `FrontolSettingsResponse` |
| Агент → сервер, пакет записан | `FrontolSettingsApplying` | `FrontolSettingsApplyingState` |

`messageType`: `FrontolSettings` = `3`, `FrontolSettingsApplying` = `4`.

JSON в примерах — camelCase, как его пишет протокол SignalR. В C# на стороне `fc` свойства называются так же, как в таблицах ниже (`DocumentKinds`, `Code`, …). Числа enum по проводу — числа, не строки. `byte[]` — строка Base64.

`FrontolSettingsApplying` агент шлёт только если записался весь пакет настроек, включая эти списки. Поле `editId` из входящего `FrontolSettings` возвращается как есть. Если запись документов не удалась, подтверждения нет: на `fc` нужен таймаут.

Если касса не смогла прочитать настройки, ответа `FrontolSettings` тоже нет.

## Списки

Четыре свойства объекта `settings`:

| Свойство | Таблица | Ключ строки |
| --- | --- | --- |
| `documentKinds` | `DOCKIND` | `code`. Строки `DOCKINDTMPL` — вложенный `documentKindTemplates`, ключ внутри вида — `code` |
| `documentTemplates` | `DOCTEMPLATE` | `code` |
| `printGroups` | `PRINTGROUP` | `code` |
| `comments` | `COMMENT` | `code` |

Повтор кода в одном списке: агент берёт последний элемент.

### `null` и пустой массив

| Значение | Что делает агент |
| --- | --- |
| свойства нет или оно `null` | эту таблицу не читает на запись и не удаляет из неё строки |
| `[]` | оставляет только то, что в массиве; остальных живых строк в таблице не будет |

Так сделано, чтобы старый `fc` без этих свойств не очистил справочники кассы. Хранить на центре «секции не было» нужно именно как `null`, не как пустой массив.

При выгрузке с кассы список есть всегда, если таблица в базе Frontol есть (в том числе пустой, если строк нет). `null` с кассы значит: таблицы в этой базе нет. Такой `null` обратно на кассу слать как `null`, не подменять на `[]`.

Списки независимы. Можно прислать только `printGroups`: виды документов не изменятся. Строки `DOCKINDTMPL` живут внутри вида. `documentKindTemplates: null` у вида не меняет его строки. `documentKindTemplates: []` удаляет строки этого вида. Вид, которого нет в `documentKinds`, удаляется вместе со своими строками. Шаблон, которого нет в `documentTemplates`, забирает строки видов, которые на него ссылались.

Строки `PRINTGROUP` и `COMMENT` с `deleted != 0` агент наверх не отдаёт и сам не удаляет: флаг нужен внутренней синхронизации Frontol. Если код такой строки есть в пакете, агент обновляет её и ставит `deleted = 0`.

## Пример

```json
{
  "agentToken": "token-agent",
  "messageType": 3,
  "editId": "edit-1",
  "settings": {
    "documentKinds": [
      {
        "code": 2,
        "name": "Продажа",
        "text": "Продажа",
        "identif": "ПР",
        "receiptType": 0,
        "askComment": false,
        "askEmployee": false,
        "copyEmployee": false,
        "askCommentOnOpen": false,
        "recompense": false,
        "documentKindTemplates": [
          {
            "code": 1,
            "templateCode": 5,
            "preview": false,
            "copyCount": 2,
            "operation": 0,
            "act": 1,
            "useCopyCount": false,
            "printEmpty": false,
            "dividePrintGroups": false,
            "cancelOnErrors": false
          }
        ]
      }
    ],
    "documentTemplates": [
      {
        "code": 1,
        "name": "Группа",
        "isFolder": true,
        "parentCode": 0,
        "minLines": 0,
        "maxLines": 0,
        "templateType": "sale",
        "printTarget": 1,
        "docOrientation": 0,
        "scriptData": "",
        "fastReport": "",
        "masterSettings": ""
      },
      {
        "code": 5,
        "name": "Чек",
        "isFolder": false,
        "parentCode": 1,
        "minLines": 0,
        "maxLines": 0,
        "templateType": "",
        "printTarget": 1,
        "docOrientation": 0,
        "scriptData": "print",
        "fastReport": "AQID",
        "masterSettings": "wizard"
      }
    ],
    "printGroups": [
      {
        "code": 3,
        "name": "Кухня",
        "text": "Кухня",
        "proposeForCheque": true,
        "proposeForCopy": false,
        "proposeForPrecheque": false,
        "closeSessionReport": false,
        "taxSystem": 2
      }
    ],
    "comments": [
      {
        "code": 6,
        "name": "Отказ",
        "text": "Отказ"
      }
    ]
  }
}
```

`fastReport: "AQID"` — Base64 байтов `01 02 03`. Пустой шаблон — `""`.

## Виды документов, `documentKinds`

| Свойство | Тип | Колонка | Ограничение |
| --- | --- | --- | --- |
| `code` | int | `CODE` | ключ |
| `name` | string | `NAME` | до 100 символов |
| `text` | string | `TEXT` | до 100 символов |
| `identif` | string | `IDENTIF` | до 3 символов |
| `receiptType` | int | `ECRRECEIPTTYPE` | числа ниже |
| `askComment` | bool | `ASKCOMMENT` | |
| `askEmployee` | bool | `ASKEMPLOYEE` | |
| `copyEmployee` | bool | `COPYEMPLOYEE` | |
| `askCommentOnOpen` | bool | `ASKCOMMENTONOPEN` | |
| `recompense` | bool | `RECOMPENSE` | |
| `documentKindTemplates` | массив или `null` | `DOCKINDTMPL` | вложенный список строк этого вида |

`documentKindTemplates` описан ниже. На виде `null` и `[]` значат то же, что для списков пакета: `null` не трогает строки этого вида, пустой массив их удаляет.

При записи агент кладёт в `NAME` и `TEXT` одну строку: `name`, если она не пустая, иначе `text`. С кассы приходят оба поля как они лежат в базе. Строка длиннее 100 символов или `identif` длиннее 3 не записывается: `Apply` возвращает ошибку, справочники документов не меняются.

`askEmployee` и `copyEmployee` в формах Frontol сейчас не используются. Агент их всё равно читает и пишет, чтобы не обнулить колонки.

`receiptType`:

| Число | Операция |
| ---: | --- |
| 0 | продажа |
| 1 | возврат |
| 4 | внесение |
| 5 | выплата |
| 6 | пользовательская |
| 8 | открытие смены |
| 9 | операция в ККМ |
| 10 | закрытие смены |
| 11 | закрытие смены платёжных систем |
| 12 | редактирование списка сотрудников |
| 13 | служебная операция |
| 14 | приход товаров |
| 15 | расход товаров |
| 16 | инвентаризация |
| 17 | переоценка |
| 18 | вскрытие тары |
| 19 | коррекция прихода |
| 20 | коррекция расхода |
| 21 | предоплата |
| 22 | возврат предоплаты |
| 23 | оплата кредита |
| 24 | возврат оплаты кредита |
| 25 | расход |
| 26 | возврат расхода |
| 27 | постановка на кран |
| 28 | снятие с крана |
| 29 | вскрытие тары (коктейли) |
| 30 | списание вскрытой тары |

## Шаблоны, `documentTemplates`

Один уровень: группа (`isFolder: true`) только в корне (`parentCode: 0`), элемент ссылается на группу её `code`. `parentCode: 0` у элемента — шаблон без группы.

Группа с `parentCode != 0` не записывается. Элемент, чей `parentCode` не указывает на группу из базы или из этого же пакета, не записывается. Уже существующая строка с этим `code` при таком пропуске не обновляется и не удаляется.

Если группа удалена этим пакетом, а элемент с ней в пакете остался, агент отвязывает элемент (`parentCode` станет `0`).

| Свойство | Тип | Колонка | Ограничение |
| --- | --- | --- | --- |
| `code` | int | `CODE` | ключ |
| `name` | string | `NAME` | до 100 символов |
| `isFolder` | bool | `ISFOLDER` | группа шаблонов |
| `parentCode` | int | `PARENTID` в базе | в пакете код родителя, не id; `0` — корень |
| `minLines` | int | `MINLINES` | для принтера документов |
| `maxLines` | int | `MAXLINES` | для принтера документов |
| `templateType` | string | `TEMPLATETYPE` | до 100 символов, реквизит группы |
| `printTarget` | int | `PRINTTARGET` | числа ниже |
| `docOrientation` | int | `DOCORIENTATION` | поворот на 180°, для подкладного |
| `scriptData` | string | `SCRIPTDATA` | текст сценария |
| `fastReport` | Base64 | `FRDATA` | шаблон FastReport |
| `masterSettings` | string | `MASTERSETTINGS` | параметры мастера |

`printTarget`:

| Число | Назначение |
| ---: | --- |
| 1 | ККМ, чековая лента |
| 2 | ККМ, подкладной документ; сейчас не используется |
| 3 | принтер чеков |
| 4 | принтер документов |

`scriptData`, `fastReport` и `masterSettings` агент записывает обратно в кассу как пришли. Для ленты, подкладного и принтера чеков (1, 2, 3) содержимое формы — `scriptData`. Для принтера документов (4) — `fastReport`.

`minLines`, `maxLines`, `templateType` и `docOrientation` агент пишет как пришли и не проверяет, что они относятся к нужному `printTarget`.

## Строки вида документа, `documentKinds[].documentKindTemplates`

Вложены в вид. Отдельного списка в `settings` нет, кода вида в строке нет: вид — это родительский элемент. `templateCode` — `code` шаблона из `documentTemplates`. Шаблон должен уже быть в базе или прийти в этом же пакете.

Нет шаблона или пара операция/действие недопустима — строка не пишется, прежняя строка этого вида с тем же `code` остаётся как была.

Вид, удалённый этим пакетом, забирает свои строки. Шаблон, удалённый этим пакетом, забирает строки, которые на него ссылались.

| Свойство | Тип | Колонка |
| --- | --- | --- |
| `code` | int | `CODE` |
| `templateCode` | int | `DOCTEMPLID` в базе, в пакете код шаблона |
| `preview` | bool | `PREVIEW` |
| `copyCount` | int | `COPYCOUNT` |
| `operation` | int | `OPERATION` |
| `act` | int | `ACT` |
| `useCopyCount` | bool | `USECOPYCOUNT` |
| `printEmpty` | bool | `PRINTEMPTY` |
| `dividePrintGroups` | bool | `DIVIDEPRINTGROUPS` |
| `cancelOnErrors` | bool | `CANCELONERRORS` |

`operation`:

| Число | Операция |
| ---: | --- |
| 0 | закрытие |
| 1 | копия |
| 2 | марка |
| 3 | откладывание |
| 4 | отмена |
| 5 | счёт |
| 6 | резервирование |

`act`:

| Число | Действие |
| ---: | --- |
| 0 | закрытие чека |
| 1 | печать копии |
| 2 | печать марки |
| 3 | печать счёта |

Агент принимает только такие пары:

| Операция | Допустимые действия |
| ---: | --- |
| 0 | 0, 1, 2 |
| 1 | 1 |
| 2 | 2 |
| 3 | 1, 2 |
| 4 | 1, 2 |
| 5 | 3 |
| 6 | 1 |

Флаги и `copyCount` агент сохраняет как пришли, даже если для выбранного действия Frontol их на форме не показывает. Для UI центра:

| Действие | Что показывать |
| ---: | --- |
| 0 | ни флаги, ни количество |
| 1 | все флаги и количество |
| 2 | только `cancelOnErrors`, без количества |
| 3 | `useCopyCount`, `preview`, `dividePrintGroups` и количество |

## Группы печати, `printGroups`

`taxGroupId` в пакете нет. У новой строки агент ставит `0`. У существующей не меняет, пока на кассу не приедет справочник налогов.

`name` и `text` при записи становятся одной строкой по тому же правилу, что у вида документа. Колонка `TEXT` — 100 символов, `NAME` — 128. Строка длиннее 100 символов не записывается: `Apply` возвращает ошибку, эта таблица не меняется.

`taxSystem` — целое из базы Frontol, агент его не разбирает. Смысл значений в базе: не используется, общая, упрощённая «доход», упрощённая «доход минус расход», единый сельскохозяйственный налог, патент. Числовую шкалу агент не задаёт: что прочитал, то и отдаёт.

| Свойство | Тип | Колонка |
| --- | --- | --- |
| `code` | int | `CODE` |
| `name` | string | `NAME` |
| `text` | string | `TEXT` |
| `proposeForCheque` | bool | `PROPOSEFORCHEQUE` |
| `proposeForCopy` | bool | `PROPOSEFORCOPY` |
| `proposeForPrecheque` | bool | `PROPOSEFORPRECHEQUE` |
| `closeSessionReport` | bool | `PRINTGROUPCLOSESESSIONREPORT` |
| `taxSystem` | int | `TAXSYSTEM` |

## Код группы в продажах

Тот же `code` группы уходит в уже существующем пакете продаж, hub-метод `SalesDocuments` (`messageType` = `9`). Новых методов нет.

`printGroupCode` — код из `TRANZT.PRINTGROUPCLOSE`, не id. `0` — группа не задана. Стыкуется с `settings.printGroups[].code`.

| Где | Свойство |
| --- | --- |
| `documents[].positions[]` | `printGroupCode` |
| `documents[].payments[]` | `printGroupCode` |

```json
{
  "positions": [
    { "wareCode": 2, "printGroupCode": 3 }
  ],
  "payments": [
    { "paymentCode": 1, "sum": 50, "printGroupCode": 3 }
  ]
}
```

## Комментарии, `comments`

`name` и `text` — одна строка при записи, каждое до 100 символов. Длиннее — ошибка `Apply`, таблица не меняется.

| Свойство | Тип | Колонка |
| --- | --- | --- |
| `code` | int | `CODE` |
| `name` | string | `NAME` |
| `text` | string | `TEXT` |

## Что сделать в `fc`

Свойства добавить в тот DTO `FrontolSettings`, который уже ходит в `FrontolSettings` / `FrontolSettingsRequest`. Отдельный `MessageType` и метод хаба не заводить.

В DTO продажи (`SalesDocuments`) добавить `printGroupCode` в позицию и в оплату.

При сохранении снимка с кассы не заменять отсутствующий список пустым. На кассу отправлять `null`, если эту секцию менять не нужно.
