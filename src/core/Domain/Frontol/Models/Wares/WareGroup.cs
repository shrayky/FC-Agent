using System.Text.Json;

namespace Domain.Frontol.Models.Wares;

public record WareGroup
{
    public string GroupCode { get; init; } = string.Empty;
    public string ParentCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public JsonElement Extra { get; init; }
}
