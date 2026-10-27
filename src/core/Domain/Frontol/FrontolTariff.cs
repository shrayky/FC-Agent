using Domain.Frontol.Models;

namespace Domain.Frontol;

public static class FrontolTariff
{
    public static bool IsActiveBasic(IReadOnlyList<LicenseInformation> licenses, DateTime utcNow) =>
        licenses.Any(license => IsBasicName(license) && IsInForce(license, utcNow));

    public static bool HasExpiredBasic(IReadOnlyList<LicenseInformation> licenses, DateTime utcNow) =>
        licenses.Any(IsBasicName) && !IsActiveBasic(licenses, utcNow);

    private static bool IsBasicName(LicenseInformation license) =>
        license.ProductName?.Contains("Frontol", StringComparison.OrdinalIgnoreCase) == true
        && string.Equals(license.TariffName?.Trim(), "Базовый", StringComparison.OrdinalIgnoreCase);

    // У бессрочной TariffDaysLeft равен -1 — это не истечение. Нет даты окончания — срок не подтверждён,
    // ограничение тарифа сохраняется. Дата в прошлом снимает тариф «Базовый».
    private static bool IsInForce(LicenseInformation license, DateTime utcNow) =>
        license.IsPerpetual
        || license.ExpiresAt is null
        || license.ExpiresAt > utcNow;
}
