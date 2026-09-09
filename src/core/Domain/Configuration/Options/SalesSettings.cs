using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class SalesSettings
{
    [JsonPropertyName("loadSales")]
    public bool LoadSales { get; set; }

    [JsonPropertyName("pollIntervalSeconds")]
    public int PollIntervalSeconds { get; set; } = 60;
}
