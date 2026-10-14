using Domain.Frontol.Models;

namespace Domain.Frontol.Interfaces;

public interface IAtolLicenseSource
{
    Task<AtolLicenseRead> Read(CancellationToken cancellationToken = default);
}
