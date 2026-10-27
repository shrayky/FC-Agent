using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class FrontolTariffService : IFrontolTariff
{
    private readonly ILogger<FrontolTariffService> _logger;
    private readonly IAtolLicenseSource _licenses;

    public FrontolTariffService(ILogger<FrontolTariffService> logger, IAtolLicenseSource licenses)
    {
        _logger = logger;
        _licenses = licenses;
    }

    public async Task<FrontolTariffState> Current(CancellationToken cancellationToken = default)
    {
        AtolLicenseRead read;
        try
        {
            read = await _licenses.Read(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Тариф лицензии Frontol не проверен: менеджер лицензий недоступен");
            return FrontolTariffState.Unverified;
        }

        // FromFiles — менеджер недоступен: в списке только имена файлов, тарифа в них нет.
        if (read.FromFiles || read.Licenses is not { Count: > 0 })
        {
            _logger.LogWarning("Тариф лицензии Frontol не проверен: список лицензий пуст или получен не от менеджера");
            return FrontolTariffState.Unverified;
        }

        var now = DateTime.UtcNow;
        if (FrontolTariff.IsActiveBasic(read.Licenses, now))
            return FrontolTariffState.ActiveBasic;

        if (FrontolTariff.HasExpiredBasic(read.Licenses, now))
            _logger.LogInformation("Лицензия Frontol «Базовый» истекла");

        return FrontolTariffState.NotBasic;
    }
}
