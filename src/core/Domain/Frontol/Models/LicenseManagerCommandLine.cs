using Domain.Messages.Dto;

namespace Domain.Frontol.Models;

public static class LicenseManagerCommandLine
{
    public static IReadOnlyList<string> Activate(string licenseId, string shopName, LicenseCompanyInfo company)
    {
        var arguments = new List<string>
        {
            "--activate", licenseId,
            "--shop", shopName
        };

        Add(arguments, "--ownership-type", company.OwnershipType);
        Add(arguments, "--company", company.Company);
        Add(arguments, "--INN", company.Inn);
        Add(arguments, "--country", company.Country);
        Add(arguments, "--region", company.Region);
        Add(arguments, "--city", company.City);
        Add(arguments, "--contact", company.Contact);
        Add(arguments, "--phone", company.Phone);
        Add(arguments, "--phone2", company.Phone2);
        Add(arguments, "--email", company.Email);
        Add(arguments, "--secret-code", company.SecretCode);
        Add(arguments, "--partner-code", company.PartnerCode);

        return arguments;
    }

    private static void Add(List<string> arguments, string option, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        arguments.Add(option);
        arguments.Add(value);
    }
}
