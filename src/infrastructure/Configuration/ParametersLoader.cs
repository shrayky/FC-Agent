using Domain.Configuration;
using Domain.Configuration.Constants;
using Shared.FilesFolders;
using Shared.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace Configuration
{
    public static class ParametersLoader
    {
        public static async Task<Result<Parameters>> LoadFromAppFolder()
        {
            var fileName = ConfigFileName();

            var loadedConfiguration = await ReadConfigurationFile(fileName);

            if (loadedConfiguration != null)
                return Result.Success(loadedConfiguration);

            var backupFileName = ConfigBackupFileName();

            loadedConfiguration = await ReadConfigurationFile(backupFileName);

            if (loadedConfiguration == null)
                return Result.Failure<Parameters>("Не удалось загрузить настройки!");

            File.Delete(fileName);
            File.Copy(backupFileName, fileName);

            return Result.Success(loadedConfiguration);
        }

        private static async Task<Parameters?> ReadConfigurationFile(string fileName)
        {
            SemaphoreSlim semaphore = new(1, 1);
            Parameters? loadedConfiguration;

            await semaphore.WaitAsync();
            
            try
            {
                if (!File.Exists(fileName))
                    return null;

                await using var fileStream = File.OpenRead(fileName);
                loadedConfiguration = await JsonSerializer.DeserializeAsync<Parameters>(fileStream, JsonSerializeOptionsProvider.Default());
            }
            catch
            {
                return null;
            }
            finally
            {
                semaphore.Release();
            }

            return loadedConfiguration;
        }

        private static string ConfigFileName()
        {
            var configFolder = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            return Path.Combine(configFolder, "config.json");
        }

        private static string ConfigBackupFileName()
        {
            var configFolder = Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

            return Path.Combine(configFolder, "config.json");
        }
    }
}
