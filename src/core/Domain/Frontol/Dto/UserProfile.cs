namespace Domain.Frontol.Dto;

public record UserProfile
{
    // код профиля
    public int Code  { get; set; }
    // название профиля
    public string Name { get; set; } = string.Empty;
    // не изменять пользователей при обмене
    public bool DontLoadUserWithThisProfile { get; set; } = true;
    // пропускать супервизор при старте
    public bool SkipSupervisorMode { get; set; } = false;
    // для режима самообслуживания
    public bool ForSelfieMode { get; set; } = false;
    // права
    public List<UserProfileSecurity> Securities { get; set; } = [];
}