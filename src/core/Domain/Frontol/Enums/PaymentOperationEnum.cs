namespace Domain.Frontol.Enums;

public enum PaymentOperationEnum
{
    Cash = 0,

    CardOrQr = 1,

    InternalPrepay = 3,

    GiftCard = 6,

    Custom = 7,

    ExternalGiftCard = 8
}
