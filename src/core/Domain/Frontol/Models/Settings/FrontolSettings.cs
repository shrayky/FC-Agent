namespace Domain.Frontol.Models.Settings;

public record FrontolSettings
{
    public GlobalControl GlobalControl { get; set; } = new();

    public List<UserProfile> UserProfiles { get; init; } = [];

    public FrontolAgentScripts Scripts { get; set; } = new();

    public List<FrontolParameter> Settings { get; set; } = [];

    // это поле означет что задание по выгрузек настроек из фронтола в центр завершено
    public bool Updated { get; set; }
}