using Domain.Frontol.Enums;

namespace Domain.Frontol.Dto;

public record UserProfileSecurity
{
    public int Id { get; set; }
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}