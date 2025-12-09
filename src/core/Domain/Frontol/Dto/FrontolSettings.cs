namespace Domain.Frontol.Dto;

public record FrontolSettings
{
    public GlobalControl GlobalControl { get; set; } = new();
    public List<UserProfile> UserProfiles { get; init; } = [];
    public bool Updated { get; set; }
}