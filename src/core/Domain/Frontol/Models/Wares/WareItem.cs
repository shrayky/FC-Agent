using System.Text.Json;

namespace Domain.Frontol.Models.Wares;

public record WareItem
{
    public int WareCode { get; init; }
    public string GroupCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public JsonElement Extra { get; init; }
}
