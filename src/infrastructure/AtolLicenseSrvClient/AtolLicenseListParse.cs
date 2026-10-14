using Domain.Frontol.Models;

namespace AtolLicenseSrvClient;

public readonly record struct AtolLicenseListParse(bool HasList, IReadOnlyList<LicenseInformation> Licenses);
