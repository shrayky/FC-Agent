namespace Domain.Agent;

/// <summary>
/// Запрещает скачивание обновления при нехватке места на диске Windows.
/// </summary>
public static class UpdateDiskGuard
{
    public const long MinFreeBytes = 500L * 1024 * 1024;

    public static bool HasEnoughSpace(long freeBytes) => freeBytes >= MinFreeBytes;
}
