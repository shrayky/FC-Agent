using Domain.Attributes;
using Domain.Frontol.Enums;

namespace Domain.Frontol.Dto;

public record GlobalControl
{
    //списание остатков
    [SettingName("ControlSaleRemain")]
    public YesNoWareEnum ControlSaleRemain { get; init; } = YesNoWareEnum.No;
    //отрицательные остатки
    [SettingName("ControlRemain")]
    public YesNoWareEnum ControlRemain { get; init; } = YesNoWareEnum.Yes;
    //продажа
    [SettingName("ControlSale")]
    public YesNoWareEnum ControlSale { get; init; } = YesNoWareEnum.Yes;
    // возврат
    [SettingName("ControlReturnAnnulate")]
    public YesNoWareEnum ControlReturnAnnulate { get; init; } = YesNoWareEnum.Yes;
    //дробное количество/весовой
    [SettingName("ControlFracSale")]
    public YesNoWareEnum ControlFracSale { get; init; } = YesNoWareEnum.Yes;
    //редактирование цены
    [SettingName("ControlEditPrice")]
    public YesNoWareEnum ControlEditPrice { get; init; }  = YesNoWareEnum.Yes;
    //запрос цены
    [SettingName("ControlInputPrice")]
    public YesNoWareEnum ControlInputPrice { get; init; } = YesNoWareEnum.No;
    //без ввода количества
    [SettingName("ControlNoNeedQuantity")]
    public YesNoWareEnum ControlNoNeedQuantity { get; init; } = YesNoWareEnum.Yes;
    //ввод количества вручную
    [SettingName("ControlManualQuantity")]
    public YesNoWareEnum ControlManualQuantity { get; init; } = YesNoWareEnum.Yes;
    // печать в документе
    [SettingName("ControlPrintOnECR")]
    public YesNoWareEnum ControlPrintOnEcr { get; init; }  = YesNoWareEnum.Yes;
    // деление упаковки
    [SettingName("ControlPackPartitioning")]
    public YesNoWareEnum ControlPackPartitioning  {get; init;} = YesNoWareEnum.No;
    // скидки
    [SettingName("ControlDiscount")]
    public YesNoWareEnum ControlDiscount { get; init; } = YesNoWareEnum.Yes;
    // запрос штрих-кода
    [SettingName("ControlInputBarcode")]
    public YesNoWareEnum ControlInputBarcode { get; init; } = YesNoWareEnum.No;
    // округление
    [SettingName("ControlRound")]
    public YesNoWareEnum ControlRound { get; init; } = YesNoWareEnum.Yes; 
    
    //срок годности
    [SettingName("ControlLife")]
    public NoWareEnum ControlLife { get; init; } = NoWareEnum.No;
    // минимальная цена
    [SettingName("ControlMinPrice")]
    public NoWareEnum ControlMinPrice {get; init;} = NoWareEnum.No;
    // максимальная скидка
    [SettingName("ControlMaxDiscount")]
    public NoWareEnum ControlMaxDiscount  {get; init;} = NoWareEnum.No;
    // кратность количества
    [SettingName("ControlQuantityPrec")]
    public NoWareEnum ControlQuantityPrec {get; init;}  = NoWareEnum.No;
    // наливаемый товар
    [SettingName("ControlDestineForBottling")]
    public NoWareEnum ControlDestineForBottling { get; init; } = NoWareEnum.Ware;
    // алкогольная продукция
    [SettingName("ControlAlco")]
    public NoWareEnum ControlAlco { get; init; } = NoWareEnum.No;
}