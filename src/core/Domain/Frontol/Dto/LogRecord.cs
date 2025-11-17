namespace Domain.Frontol.Dto;

public record LogRecord
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string LogMessage { get; set; } = string.Empty;
}