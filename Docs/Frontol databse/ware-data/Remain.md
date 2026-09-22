# Remain/Остатки по разрезам

## Структура таблицы

| № | Имя поля | Тип | Длина | Назначение |
| ---: | --- | --- | ---: | --- |
| 1 | ID | Numeric | 18 | Уникальный идентификатор |
| 2 | WareID | Integer | | ID товара |
| 3 | AspectValue1ID | Integer | | ID 1-го разреза |
| 4 | AspectValue2ID | Integer | | ID 2-го разреза |
| 5 | AspectValue3ID | Integer | | ID 3-го разреза |
| 6 | AspectValue4ID | Integer | | ID 4-го разреза |
| 7 | AspectValue5ID | Integer | | ID 5-го разреза |
| 8 | Deleted | Integer | | Признак удаления записи |
| 9 | CHNG | Numeric | 18 | Счетчик изменений |
| 10 | BDOCode | Integer | | Код БД, в которой произведены изменения |
| 11 | UseRemain | Integer | | Учет остатков |
| 12 | OwnerBDO | Integer | | Код БД, которой передали записи при персональной синхронизации |
| 13 | InsChng | Numeric | 18 | Счетчик вставок |
| 14 | EnterpriseID | Integer | | ID предприятия, по умолчанию 0 |
| 15 | PriceType | Integer | | Тип цены. Если не заполнен, то null |
