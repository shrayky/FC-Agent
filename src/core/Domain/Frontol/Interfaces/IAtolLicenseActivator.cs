using CSharpFunctionalExtensions;
using Domain.Messages.Dto;

namespace Domain.Frontol.Interfaces;

public interface IAtolLicenseActivator
{
    Task<Result> Activate(string licenseId, string shopName, LicenseCompanyInfo company);
}
