using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ViewApp.Pages;

public class SettingsModel : PageModel
{
    private readonly IParametersService _parametersService;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(IParametersService parametersService, ILogger<SettingsModel> logger)
    {
        _parametersService = parametersService;
        _logger = logger;
    }

    [BindProperty]
    public Parameters AppSettings { get; set; } = new();

    public string[] LogLevels { get; } = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public string? Message { get; set; }

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

        return Page();
    }
}
