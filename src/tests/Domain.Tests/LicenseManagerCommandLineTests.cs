using Domain.Frontol.Models;
using Domain.Messages.Dto;

namespace Domain.Tests;

[TestFixture]
public class LicenseManagerCommandLineTests
{
    [Test]
    public void Activate_собирает_команду_с_данными_компании()
    {
        var arguments = LicenseManagerCommandLine.Activate("8SL64-OZEN4-6TLPA-B697W", "Металлургов-Лазо", Company());

        Assert.That(arguments, Is.EqualTo(new[]
        {
            "--activate", "8SL64-OZEN4-6TLPA-B697W",
            "--shop", "Металлургов-Лазо",
            "--ownership-type", "ИП",
            "--company", "Жарков А.Е.",
            "--INN", "246104058720",
            "--country", "Россия",
            "--region", "Красноярский край",
            "--city", "г Красноярск",
            "--contact", "Жарков А.Е.",
            "--phone", "+79135506260",
            "--email", "shrayky@gmail.com",
            "--secret-code", "1234"
        }));
    }

    [Test]
    public void Activate_добавляет_заполненные_необязательные_поля()
    {
        var company = Company();
        company.Phone2 = "+79130000000";
        company.PartnerCode = "12345";

        var arguments = LicenseManagerCommandLine.Activate("LID", "shop", company).ToList();

        Assert.That(arguments[arguments.IndexOf("--phone2") + 1], Is.EqualTo("+79130000000"));
        Assert.That(arguments[arguments.IndexOf("--partner-code") + 1], Is.EqualTo("12345"));
    }

    [Test]
    public void Activate_пропускает_пустые_необязательные_поля()
    {
        var company = Company();
        company.Phone2 = "   ";
        company.PartnerCode = "";

        var arguments = LicenseManagerCommandLine.Activate("LID", "shop", company);

        Assert.That(arguments, Does.Not.Contain("--phone2"));
        Assert.That(arguments, Does.Not.Contain("--partner-code"));
    }

    private static LicenseCompanyInfo Company() => new()
    {
        OwnershipType = "ИП",
        Company = "Жарков А.Е.",
        Inn = "246104058720",
        Country = "Россия",
        Region = "Красноярский край",
        City = "г Красноярск",
        Contact = "Жарков А.Е.",
        Phone = "+79135506260",
        Email = "shrayky@gmail.com",
        SecretCode = "1234"
    };
}
