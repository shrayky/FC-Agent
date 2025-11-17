namespace Domain.Configuration.Options;

public class CentralServerConnection
{
    public string Address {  get; set; } = string.Empty;
    public string Token {  get; set; } = string.Empty;
    public bool DownloadNewVersion { get; set; } = false;
}