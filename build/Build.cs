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

    [Parameter("Архитектура пакета: x86 или x64. По умолчанию x86, как agents-builds.")]
    readonly string Architecture = "x86";

    [Parameter("Версия .NET / ASP.NET Runtime для скачивания, например 10.0.11")]
    readonly string RuntimeVersion = "10.0.11";

    [Parameter("Готовый aspnetcore-runtime-*-win-*.exe; если пусто — скачиваем")]
    readonly string RuntimeInstaller = default!;

    [Parameter("Готовый dotnet-runtime-*-win-*.exe; если пусто — скачиваем")]
    readonly string DotNetRuntimeInstaller = default!;

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
            var installers = await EnsureRuntimeInstallersAsync();
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
            foreach (var installer in installers)
                CopyItem(installer, runtimeDir / installer.Name);

            var rtZip = BuildsDirectory / $"{version}_{arch}_windows_rt.zip";
            ZipStaging(staging, rtZip, deleteStaging: true);

            Serilog.Log.Information("Archives successfully created:");
            Serilog.Log.Information("- {Zip}", nortZip);
            Serilog.Log.Information("- {Zip}", rtZip);
        });

    /// <summary>
    /// Публикует host Native AOT — нативный exe без JIT, стартует без установленного .NET.
    /// PublishSingleFile нельзя: AOT уже даёт один native-файл.
    /// </summary>
    void PublishHost(string rid)
    {
        HostPublish.CreateOrCleanDirectory();
        DotNetPublish(s => s
            .SetProject(HostProject)
            .SetConfiguration("Release")
            .SetRuntime(rid)
            .SetSelfContained(true)
            .SetPublishSingleFile(false)
            .SetProperty("PublishAot", "true")
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
    /// Берёт готовые установщики или скачивает .NET Runtime и ASP.NET Core Runtime.
    /// </summary>
    async Task<IReadOnlyList<AbsolutePath>> EnsureRuntimeInstallersAsync()
    {
        var rid = RuntimeIdentifier();
        var cacheDir = BuildsDirectory / "runtime";
        cacheDir.CreateDirectory();

        var dotnetRuntime = await EnsureInstallerAsync(
            DotNetRuntimeInstaller,
            $"dotnet-runtime-{RuntimeVersion}-{rid}.exe",
            $"https://builds.dotnet.microsoft.com/dotnet/Runtime/{RuntimeVersion}/dotnet-runtime-{RuntimeVersion}-{rid}.exe",
            cacheDir);

        var aspNetRuntime = await EnsureInstallerAsync(
            RuntimeInstaller,
            $"aspnetcore-runtime-{RuntimeVersion}-{rid}.exe",
            $"https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/{RuntimeVersion}/aspnetcore-runtime-{RuntimeVersion}-{rid}.exe",
            cacheDir);

        return [dotnetRuntime, aspNetRuntime];
    }

    /// <summary>
    /// Возвращает готовый exe или скачивает его в builds/runtime.
    /// </summary>
    async Task<AbsolutePath> EnsureInstallerAsync(
        string providedPath,
        string fileName,
        string url,
        AbsolutePath cacheDir)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            AbsolutePath path = providedPath;
            Assert.True(path.FileExists(), $"Не найден установщик runtime: {path}");
            return path;
        }

        var cached = cacheDir / fileName;
        if (cached.FileExists())
            return cached;

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
            throw new InvalidOperationException($"Не удалось скачать runtime: {url}", ex);
        }

        Assert.True(cached.FileExists() && new FileInfo(cached).Length > 0,
            $"Скачанный установщик пустой: {cached}");
        return cached;
    }

    string RuntimeIdentifier() => NormalizedArchitecture() == "x86" ? "win-x86" : "win-x64";

    string NormalizedArchitecture()
    {
        var value = string.IsNullOrWhiteSpace(Architecture) ? "x86" : Architecture.Trim().ToLowerInvariant();
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
