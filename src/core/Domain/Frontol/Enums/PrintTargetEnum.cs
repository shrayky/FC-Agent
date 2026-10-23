namespace Domain.Frontol.Enums;

// DOCTEMPLATE.PRINTTARGET: 1 — чековая лента ККМ, 2 — подкладной документ, 3 — принтер чеков, 4 — принтер документов.
public enum PrintTargetEnum
{
    EcrTape = 1,

    EcrSlip = 2,

    ReceiptPrinter = 3,

    DocumentPrinter = 4,
}
