namespace Domain.Frontol.Models.Settings;

public record UserProfile
{
    public int Code  { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool DontLoadUserWithThisProfile { get; set; } = true;

    public bool SkipSupervisorMode { get; set; } = false;

    public bool ForSelfieMode { get; set; } = false;

    public List<UserProfileSecurity> Securities { get; set; } = [];
}

public record UserProfileSecurity
{
    public int Id { get; set; }
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}