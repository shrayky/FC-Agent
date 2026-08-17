namespace Domain.Frontol.Models.Settings;

public record FrontolAgentScripts
{
    public ActionScript FrontolScript { get; set; } = new();

    public List<AtolCashRegisterDriver10Srcipt> CashRegisterDriver10Scripts { get; set; } = [];

    public bool UploadCashRegisterScripts { get; set; }
}

public record AtolCashRegisterDriver10Srcipt
{
    public string FileName { get; init; } = string.Empty;

    public string Script { get; init; } = string.Empty;

    public bool CanDelete { get; init; }
}

public record ActionScript
{
    public int Code = 99999;

    public string Name { get; init; } = "main";

    public string Text { get; set; } = string.Empty;
}