using System.Text.Json.Serialization;

namespace Domain.Configuration.Options
{
    public class DatabaseConnection
    {
        [JsonPropertyName("databasePath")]
        public string DatabasePath { get; set; } = string.Empty;
        
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = "SYSDBA";
        
        [JsonPropertyName("password")]
        public string Password { get; set; } = "masterkey";
    }
}
