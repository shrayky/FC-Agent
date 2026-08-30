using HostApp.Installer;

namespace HostApp.Tests;

[TestFixture]
public class AspNetRuntimeSetupTests
{
    /// <summary>
    /// В лёгком пакете папки runtime нет — установщики не ищем.
    /// </summary>
    [Test]
    public void FindInstallers_пусто_если_папки_нет()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-pack-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            Assert.That(AspNetRuntimeSetup.FindInstallers(root), Is.Empty);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Полный пакет кладёт exe в runtime\.
    /// </summary>
    [Test]
    public void FindInstallers_находит_exe_в_runtime()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-pack-" + Guid.NewGuid());
        var runtime = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtime);
        var exe = Path.Combine(runtime, "aspnetcore-runtime-10.0.2-win-x64.exe");
        File.WriteAllBytes(exe, [0]);
        try
        {
            Assert.That(AspNetRuntimeSetup.FindInstallers(root), Is.EqualTo(new[] { exe }));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// .NET Runtime ставится до ASP.NET Core — без hostfxr apphost не стартует.
    /// </summary>
    [Test]
    public void FindInstallers_dotnet_runtime_перед_aspnetcore()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-pack-" + Guid.NewGuid());
        var runtime = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtime);
        var aspnet = Path.Combine(runtime, "aspnetcore-runtime-10.0.11-win-x86.exe");
        var dotnet = Path.Combine(runtime, "dotnet-runtime-10.0.11-win-x86.exe");
        File.WriteAllBytes(aspnet, [0]);
        File.WriteAllBytes(dotnet, [0]);
        try
        {
            Assert.That(
                AspNetRuntimeSetup.FindInstallers(root),
                Is.EqualTo(new[] { dotnet, aspnet }));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Одного AspNetCore мало: без Microsoft.NETCore.App apphost не находит .NET.
    /// </summary>
    [Test]
    public void NeedsInstall_true_если_только_AspNetCore()
    {
        Assert.That(
            AspNetRuntimeSetup.NeedsInstall(["Microsoft.AspNetCore.App/10.0.11"]),
            Is.True);
    }

    /// <summary>
    /// Оба shared framework 10 закрывают требование — установщики не запускаем.
    /// </summary>
    [Test]
    public void NeedsInstall_false_если_оба_10_уже_есть()
    {
        Assert.That(
            AspNetRuntimeSetup.NeedsInstall(
            [
                "Microsoft.NETCore.App/10.0.11",
                "Microsoft.AspNetCore.App/10.0.11"
            ]),
            Is.False);
    }

    /// <summary>
    /// 0 — успех, 3010 — успех с запросом перезагрузки.
    /// </summary>
    [Test]
    public void IsSuccessExitCode_принимает_0_и_3010()
    {
        Assert.That(AspNetRuntimeSetup.IsSuccessExitCode(0), Is.True);
        Assert.That(AspNetRuntimeSetup.IsSuccessExitCode(3010), Is.True);
        Assert.That(AspNetRuntimeSetup.IsSuccessExitCode(1), Is.False);
    }
}
