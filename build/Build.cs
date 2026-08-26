using Domain.Configuration.Constants;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Release);

    [Parameter("Версия архива, например 2-3. По умолчанию из ApplicationInformation.")]
    readonly string Version = default!;

    [Parameter("Архитектура пакета: x64 или x86")]
    readonly string Architecture = "x64";

    [Parameter("Версия ASP.NET Core Runtime для скачивания, например 10.0.2")]
    readonly string RuntimeVersion = "10.0.2";

    [Parameter("Готовый aspnetcore-runtime-*-win-*.exe; если пусто — скачиваем")]
    readonly string RuntimeInstaller = default!;

    AbsolutePath HostProject => RootDirectory / "src" / "presentation" / "HostApp" / "HostApp.csproj";
    AbsolutePath AgentProject => RootDirectory / "src" / "presentation" / "ViewApp" / "ViewApp.csproj";
    AbsolutePath BuildsDirectory => RootDirectory / "builds";

    AbsolutePath HostPublish => BuildsDirectory / "host-win";
    AbsolutePath AgentPublish => BuildsDirectory / "agent-win";

    string? _archiveVersion;

    Target PublishWindows => _ => _
        .Executes(() =>
        {
            var rid = RuntimeIdentifier();
            PublishHost(rid);
            PublishAgent(rid);
        });

    Target Release => _ => _
        .DependsOn(PublishWindows)
        .Executes(async () =>
        {
            var installer = await EnsureRuntimeInstallerAsync();
            var version = EnsureArchiveVersion();
            var arch = NormalizedArchitecture();
            var productFolder = $"{ApplicationInformation.Version}.{ApplicationInformation.Assembly}";

            var staging = CreateCleanStaging();
            PackHost(staging);
            PackAgent(staging, productFolder);

            var nortZip = BuildsDirectory / $"{version}_{arch}_windows_nort.zip";
            ZipStaging(staging, nortZip, deleteStaging: false);

            var runtimeDir = staging / "runtime";
            runtimeDir.CreateDirectory();
            CopyItem(installer, runtimeDir / installer.Name);

            var rtZip = BuildsDirectory / $"{version}_{arch}_windows_rt.zip";
            ZipStaging(staging, rtZip, deleteStaging: true);

            Serilog.Log.Information("Archives successfully created:");
            Serilog.Log.Information("- {Zip}", nortZip);
            Serilog.Log.Information("- {Zip}", rtZip);
        });

    /// <summary>
    /// Публикует host как self-contained single-file — он должен стартовать без системного runtime.
    /// </summary>
    void PublishHost(string rid)
    {
        HostPublish.CreateOrCleanDirectory();
        DotNetPublish(s => s
            .SetProject(HostProject)
            .SetConfiguration("Release")
            .SetRuntime(rid)
            .SetSelfContained(true)
            .SetPublishSingleFile(true)
            .SetOutput(HostPublish));
    }

    /// <summary>
    /// Публикует fc-agent как framework-dependent single-file — ставится после runtime.
    /// </summary>
    void PublishAgent(string rid)
    {
        AgentPublish.CreateOrCleanDirectory();
        DotNetPublish(s => s
            .SetProject(AgentProject)
            .SetConfiguration("Release")
            .SetRuntime(rid)
            .SetSelfContained(false)
            .SetPublishSingleFile(true)
            .SetProperty("EnableCompressionInSingleFile", false)
            .SetOutput(AgentPublish));
    }

    /// <summary>
    /// Кладёт fc.exe в корень пакета.
    /// </summary>
    void PackHost(AbsolutePath staging)
    {
        var source = HostPublish / "fc.exe";
        Assert.True(source.FileExists(), $"Не найден host: {source}");
        CopyItem(source, staging / "fc.exe");
    }

    /// <summary>
    /// Кладёт опубликованный fc-agent в fc-agent/{version.assembly}/.
    /// </summary>
    void PackAgent(AbsolutePath staging, string productFolder)
    {
        var exe = AgentPublish / "fc-agent.exe";
        Assert.True(exe.FileExists(), $"Не найден агент: {exe}");

        var target = staging / "fc-agent" / productFolder;
        target.CreateDirectory();
        CopyItem(AgentPublish, target);
    }

    /// <summary>
    /// Берёт готовый установщик runtime или скачивает его в builds/runtime.
    /// </summary>
    async Task<AbsolutePath> EnsureRuntimeInstallerAsync()
    {
        if (!string.IsNullOrWhiteSpace(RuntimeInstaller))
        {
            AbsolutePath path = RuntimeInstaller;
            Assert.True(path.FileExists(), $"Не найден установщик runtime: {path}");
            return path;
        }

        var rid = RuntimeIdentifier();
        var fileName = $"aspnetcore-runtime-{RuntimeVersion}-{rid}.exe";
        var cacheDir = BuildsDirectory / "runtime";
        cacheDir.CreateDirectory();
        var cached = cacheDir / fileName;
        if (cached.FileExists())
            return cached;

        var url =
            $"https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/{RuntimeVersion}/{fileName}";
        Serilog.Log.Information("Скачиваю {Url}", url);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(cached);
            await input.CopyToAsync(output);
        }
        catch (Exception ex)
        {
            if (cached.FileExists())
                cached.DeleteFile();
            throw new InvalidOperationException($"Не удалось скачать ASP.NET Runtime: {url}", ex);
        }

        Assert.True(cached.FileExists() && new FileInfo(cached).Length > 0,
            $"Скачанный установщик пустой: {cached}");
        return cached;
    }

    string RuntimeIdentifier() => NormalizedArchitecture() == "x86" ? "win-x86" : "win-x64";

    string NormalizedArchitecture()
    {
        var value = string.IsNullOrWhiteSpace(Architecture) ? "x64" : Architecture.Trim().ToLowerInvariant();
        Assert.True(value is "x64" or "x86", $"Неизвестная архитектура: {Architecture}");
        return value;
    }

    /// <summary>
    /// Версия имени zip: параметр --Version или Version-Assembly из Domain.
    /// </summary>
    string EnsureArchiveVersion()
    {
        if (!string.IsNullOrWhiteSpace(_archiveVersion))
            return _archiveVersion;

        _archiveVersion = string.IsNullOrWhiteSpace(Version)
            ? $"{ApplicationInformation.Version}-{ApplicationInformation.Assembly}"
            : Version.Trim();

        Assert.False(string.IsNullOrWhiteSpace(_archiveVersion), "Пустая версия архива.");
        return _archiveVersion!;
    }

    AbsolutePath CreateCleanStaging()
    {
        var staging = TemporaryDirectory / "fc-agent-archive";
        staging.CreateOrCleanDirectory();
        return staging;
    }

    /// <summary>
    /// Упаковывает staging в zip; каталог удаляет только после второго архива.
    /// </summary>
    void ZipStaging(AbsolutePath staging, AbsolutePath zip, bool deleteStaging)
    {
        BuildsDirectory.CreateDirectory();
        if (zip.FileExists())
            zip.DeleteFile();
        staging.ZipTo(zip);
        if (deleteStaging)
            staging.DeleteDirectory();
        Serilog.Log.Information("Создан {Zip}", zip);
    }

    /// <summary>
    /// Копирует файл или каталог в назначение.
    /// </summary>
    static void CopyItem(AbsolutePath source, AbsolutePath destination)
    {
        if (source.FileExists())
        {
            destination.Parent.CreateDirectory();
            File.Copy(source, destination, overwrite: true);
            return;
        }

        Assert.True(source.DirectoryExists(), $"Не найден каталог: {source}");
        destination.CreateDirectory();

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = destination / relative;
            target.Parent.CreateDirectory();
            File.Copy(file, target, overwrite: true);
        }
    }
}
