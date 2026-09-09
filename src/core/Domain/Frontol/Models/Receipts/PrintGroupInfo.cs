namespace Domain.Frontol.Models.Receipts;

public record PrintGroupInfo
{
    public int Id { get; init; }

    public int Code { get; init; }

    public string Name { get; init; } = string.Empty;
}
