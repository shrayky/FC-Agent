using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ViewApp.Pages;

public class SettingsModel : PageModel
{
    private readonly IParametersService _parametersService;
    private readonly IAtolLicenseSource _licenses;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(IParametersService parametersService, IAtolLicenseSource licenses, ILogger<SettingsModel> logger)
    {
        _parametersService = parametersService;
        _licenses = licenses;
        _logger = logger;
    }

    [BindProperty]
    public Parameters AppSettings { get; set; } = new();

    public string[] LogLevels { get; } = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public string? Message { get; set; }

    public AtolLicenseRead LicenseRead { get; private set; } = new([], false);

    public async Task OnGetAsync()
    {
        try
        {
            AppSettings = await _parametersService.Current();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке настроек");
            Message = $"Ошибка при загрузке настроек: {ex.Message}";
        }

        await LoadLicenses();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var saved = await _parametersService.Update(AppSettings);
            Message = saved ? "Настройки сохранены." : "Не удалось сохранить настройки.";
            AppSettings = await _parametersService.Current();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении настроек");
            Message = $"Ошибка: {ex.Message}";
            AppSettings = await _parametersService.Current();
        }

        await LoadLicenses();
        return Page();
    }

    public static string ExpiresText(LicenseInformation license) =>
        license.IsPerpetual
            ? "бессрочно"
            : license.ExpiresAt?.ToLocalTime().ToString("dd.MM.yyyy") ?? "—";

    public static string DaysText(LicenseInformation license)
    {
        if (license.IsPerpetual)
            return "бессрочно";

        if (license.ExpiresAt is null)
            return "—";

        return license.TariffDaysLeft < 0 ? "истекла" : $"{license.TariffDaysLeft} дн.";
    }

    public static string TariffText(LicenseInformation license)
    {
        if (!license.IsTrial)
            return license.TariffName;

        return license.TariffName.Length == 0 ? "пробная" : $"пробная, {license.TariffName}";
    }

    public static string LicenseRowClass(LicenseInformation license)
    {
        if (license.IsPerpetual || license.ExpiresAt is null)
            return string.Empty;

        if (license.TariffDaysLeft < 0)
            return "license-expired";

        return license.TariffDaysLeft <= 30 ? "license-soon" : string.Empty;
    }

    private async Task LoadLicenses()
    {
        try
        {
            LicenseRead = await _licenses.Read();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения лицензий АТОЛ");
            LicenseRead = new AtolLicenseRead([], true);
        }
    }
}
