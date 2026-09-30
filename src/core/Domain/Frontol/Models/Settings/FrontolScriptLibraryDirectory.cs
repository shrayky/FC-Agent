namespace Domain.Frontol.Models.Settings;

// Корень каталога — каталог данных агента из Shared.FilesFolders.Folders (ProgramData\<manufacture>\<app>),
// поэтому здесь только имя подкаталога библиотек.
public static class FrontolScriptLibraryDirectory
{
    public const string FolderName = "fScript";

    public const string ConstantName = "libPath";

    public const int HeaderLines = 20;

    public static string InAgentDataFolder(string agentDataFolder) => Path.Combine(agentDataFolder, FolderName);
}
