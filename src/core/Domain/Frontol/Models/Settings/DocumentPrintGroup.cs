namespace Domain.Frontol.Models.Settings;

public record DocumentPrintGroup
{
    public int Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool ProposeForCheque { get; set; }

    public bool ProposeForCopy { get; set; }

    public bool ProposeForPrecheque { get; set; }

    public bool CloseSessionReport { get; set; }

    public int TaxSystem { get; set; }
}
