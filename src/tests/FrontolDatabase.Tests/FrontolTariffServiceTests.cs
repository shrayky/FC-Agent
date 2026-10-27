using Application.Frontol;
using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolTariffServiceTests
{
    [Test]
    public async Task Пустой_список_тариф_не_проверен()
    {
        var state = await Read(new AtolLicenseRead([], false));

        Assert.That(state, Is.EqualTo(FrontolTariffState.Unverified));
    }

    [Test]
    public async Task Список_не_от_менеджера_тариф_не_проверен()
    {
        var state = await Read(new AtolLicenseRead(
            [new LicenseInformation { ProductName = "FRONTOL", TariffName = "Базовый" }],
            true));

        Assert.That(state, Is.EqualTo(FrontolTariffState.Unverified));
    }

    [Test]
    public async Task Сбой_чтения_тариф_не_проверен()
    {
        var licenses = new Mock<IAtolLicenseSource>();
        licenses.Setup(m => m.Read(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("нет связи"));

        var state = await Service(licenses.Object).Current();

        Assert.That(state, Is.EqualTo(FrontolTariffState.Unverified));
    }

    [Test]
    public async Task Действующий_Базовый()
    {
        var state = await Read(new AtolLicenseRead(
        [
            new LicenseInformation
            {
                ProductName = "FRONTOL",
                TariffName = "Базовый",
                ExpiresAt = DateTime.UtcNow.AddDays(10)
            }
        ], false));

        Assert.That(state, Is.EqualTo(FrontolTariffState.ActiveBasic));
    }

    [Test]
    public async Task Истекший_Базовый_не_блокирует()
    {
        var state = await Read(new AtolLicenseRead(
        [
            new LicenseInformation
            {
                ProductName = "FRONTOL",
                TariffName = "Базовый",
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            }
        ], false));

        Assert.That(state, Is.EqualTo(FrontolTariffState.NotBasic));
    }

    private static Task<FrontolTariffState> Read(AtolLicenseRead read)
    {
        var licenses = new Mock<IAtolLicenseSource>();
        licenses.Setup(m => m.Read(It.IsAny<CancellationToken>())).ReturnsAsync(read);
        return Service(licenses.Object).Current();
    }

    private static FrontolTariffService Service(IAtolLicenseSource licenses) =>
        new(new Mock<ILogger<FrontolTariffService>>().Object, licenses);
}
