# BarCode/Штрихкоды

## Структура таблицы

| № | Имя поля | Тип | Длина | Назначение |
| ---: | --- | --- | ---: | --- |
| 1 | ID | Integer | | Уникальный идентификатор |
| 2 | WareID | Integer | | ID товара |
| 3 | Barcode | Varchar | 40 | Штрихкод |
| 4 | AspectValue1ID | Integer | | ID 1-го разреза |
| 5 | AspectValue2ID | Integer | | ID 2-го разреза |
| 6 | AspectValue3ID | Integer | | ID 3-го разреза |
| 7 | AspectValue4ID | Integer | | ID 4-го разреза |
| 8 | AspectValue5ID | Integer | | ID 5-го разреза |
| 9 | Factor | Double Precision | | Коэффициент |
| 10 | Deleted | Integer | | Признак удаления записи |
| 11 | BDOCode | Integer | | Код БД, в которой произведены изменения |
| 12 | CHNG | Numeric | 18 | Счетчик изменений |
| 13 | OwnerBDO | Integer | | Код БД, которой передали записи при персональной синхронизации |
| 14 | InsChng | Numeric | 18 | Счетчик вставок |
