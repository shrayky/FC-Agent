using Domain.Frontol;
using Domain.Frontol.Models;

namespace Domain.Tests;

[TestFixture]
public class FrontolTariffTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Действующий_Базовый_Frontol_учитывается_лицензия_ККТ_нет()
    {
        var licenses = new[]
        {
            new LicenseInformation
            {
                ProductName = "frontol xPOS",
                TariffName = " базовый ",
                ExpiresAt = Now.AddDays(10)
            },
            new LicenseInformation { ProductName = "ККТ", TariffName = "Базовый", ExpiresAt = Now.AddDays(10) }
        };

        Assert.That(FrontolTariff.IsActiveBasic(licenses, Now), Is.True);
        Assert.That(FrontolTariff.HasExpiredBasic(licenses, Now), Is.False);
    }

    [Test]
    public void Истекший_Базовый_не_действует()
    {
        LicenseInformation[] licenses =
        [
            new()
            {
                ProductName = "FRONTOL",
                TariffName = "Базовый",
                ExpiresAt = Now.AddDays(-1)
            }
        ];

        Assert.That(FrontolTariff.IsActiveBasic(licenses, Now), Is.False);
        Assert.That(FrontolTariff.HasExpiredBasic(licenses, Now), Is.True);
    }

    [Test]
    public void Бессрочный_Базовый_действует()
    {
        LicenseInformation[] licenses =
        [
            new()
            {
                ProductName = "FRONTOL",
                TariffName = "Базовый",
                IsPerpetual = true,
                TariffDaysLeft = -1
            }
        ];

        Assert.That(FrontolTariff.IsActiveBasic(licenses, Now), Is.True);
        Assert.That(FrontolTariff.HasExpiredBasic(licenses, Now), Is.False);
    }

    [Test]
    public void Нет_даты_окончания_тариф_действует()
    {
        LicenseInformation[] licenses =
        [
            new() { ProductName = "FRONTOL", TariffName = "Базовый" }
        ];

        Assert.That(FrontolTariff.IsActiveBasic(licenses, Now), Is.True);
    }

    [Test]
    public void Другой_тариф_Frontol_и_Базовый_ККТ_не_блокируют()
    {
        LicenseInformation[] licenses =
        [
            new() { ProductName = "FRONTOL", TariffName = "Оптимальный", ExpiresAt = Now.AddDays(10) },
            new() { ProductName = "АТОЛ ККТ", TariffName = "Базовый", ExpiresAt = Now.AddDays(10) }
        ];

        Assert.That(FrontolTariff.IsActiveBasic(licenses, Now), Is.False);
        Assert.That(FrontolTariff.HasExpiredBasic(licenses, Now), Is.False);
    }
}
