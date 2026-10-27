using Domain.Frontol;

namespace Domain.Frontol.Interfaces;

public interface IFrontolTariff
{
    Task<FrontolTariffState> Current(CancellationToken cancellationToken = default);
}
