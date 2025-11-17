using Domain.Configuration.Constants;
using Domain.Entitys.Logs.Dto;
using Domain.Entitys.Logs.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;
using Shared.FilesFolders;

namespace Logger.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class LogCollectorService : ILogCollectorService
    {

        private readonly string _logFileName = $"{ApplicationInformation.Name.ToLower()}";
        private readonly string _logsFolderPath = Folders.LogFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name);

        public async Task<LogPacket> Collect()
        {
            return await Collect("");
        }

        public async Task<LogPacket> Collect(string selectedFileName)
        {
            LogPacket packet = new();       

            if (!Directory.Exists(_logsFolderPath))
                return new();

            var files = Directory.EnumerateFiles(_logsFolderPath, $"{_logFileName}*.log");

            if (!files.Any())
                return new();

            var uploadLogFileName = string.Empty;
            var nowFileName = string.Empty;
            var fileNameWithoutPrefix = string.Empty;

            foreach (var file in files)
            {
                fileNameWithoutPrefix = Path.GetFileNameWithoutExtension(file).Replace(_logFileName, "");

                packet.LogFileNames.Add(fileNameWithoutPrefix);

                if (selectedFileName == string.Empty)
                    continue;

                if (fileNameWithoutPrefix == selectedFileName)
                    uploadLogFileName = file;

                nowFileName = file;
            }

            if (selectedFileName == "now")
            {
                uploadLogFileName = nowFileName;
                selectedFileName = fileNameWithoutPrefix;
            }

            packet.LogText = await ReadLogFileAsync(uploadLogFileName);
            packet.SelectedLogFileName = selectedFileName;
            
            return packet;

        }

        private async Task<string> ReadLogFileAsync(string logFileName)
        {
            if (string.IsNullOrEmpty(logFileName))
                return string.Empty;

            var tempLog = Path.Combine(Path.GetDirectoryName(logFileName) ?? string.Empty, "temp_slog.txt");

            try
            {
                File.Copy(logFileName, tempLog, true);
            }
            catch
            {
                return string.Empty;
            }

            var log = await File.ReadAllTextAsync(tempLog);

            File.Delete(tempLog);

            return log;
        }
    }
}
