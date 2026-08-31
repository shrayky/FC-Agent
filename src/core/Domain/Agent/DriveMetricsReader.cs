namespace Domain.Agent;

/// <summary>
/// Размеры дисков и файлов для отправки в fc и проверки места под обновление.
/// </summary>
public static class DriveMetricsReader
{
    public static string LocalFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var parts = path.Split(':');
        if (parts.Length >= 3)
            return $"{parts[1]}:{string.Join(":", parts.Skip(2))}";

        return path;
    }

    public static long FileSize(string path)
    {
        try
        {
            var localPath = LocalFilePath(path);
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                return 0;

            return new FileInfo(localPath).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static DriveMetrics FromPath(string path)
    {
        try
        {
            var localPath = LocalFilePath(path);
            var root = Path.GetPathRoot(localPath);
            if (string.IsNullOrWhiteSpace(root))
                return new DriveMetrics(0, 0);

            var letter = DriveLetter(root);
            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                return new DriveMetrics(0, 0, letter);

            return new DriveMetrics(drive.TotalSize, drive.AvailableFreeSpace, letter, drive.VolumeLabel);
        }
        catch (Exception)
        {
            return new DriveMetrics(0, 0);
        }
    }

    public static DriveMetrics WindowsSystemDrive()
    {
        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return FromPath(windowsFolder);
    }

    public static string DriveLetter(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !char.IsLetter(root[0]))
            return string.Empty;

        return char.ToUpperInvariant(root[0]).ToString();
    }
}
