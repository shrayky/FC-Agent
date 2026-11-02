using System.Globalization;

namespace Domain.Frontol;

public static class FrontolMaintenanceNotice
{
    public static string Format(DateTime resumeAtUtc, DateTime nowLocal)
    {
        var local = DateTime.SpecifyKind(resumeAtUtc, DateTimeKind.Utc).ToLocalTime();
        var clock = local.Date == nowLocal.Date
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);

        return $"Сервисное обслуживание: база отключена до {clock}. Команды в базу не выполняются.";
    }
}
