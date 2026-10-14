namespace Domain.Frontol.Models;

public sealed record AtolLicenseRead(IReadOnlyList<LicenseInformation> Licenses, bool FromFiles);
