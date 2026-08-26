using HostApp.Installer;

namespace HostApp.Tests;

[TestFixture]
public class AspNetRuntimeSetupTests
{
    /// <summary>
    /// В лёгком пакете папки runtime нет — установщик не ищем.
    /// </summary>
    [Test]
    public void FindInstaller_null_если_папки_нет()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-pack-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            Assert.That(AspNetRuntimeSetup.FindInstaller(root), Is.Null);
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
    public void FindInstaller_находит_exe_в_runtime()
    {
        var root = Path.Combine(Path.GetTempPath(), "fc-pack-" + Guid.NewGuid());
        var runtime = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtime);
        var exe = Path.Combine(runtime, "aspnetcore-runtime-10.0.2-win-x64.exe");
        File.WriteAllBytes(exe, [0]);
        try
        {
            Assert.That(AspNetRuntimeSetup.FindInstaller(root), Is.EqualTo(exe));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Если ASP.NET 10 уже стоит, установщик не запускаем.
    /// </summary>
    [Test]
    public void NeedsInstall_false_если_10_уже_есть()
    {
        Assert.That(
            AspNetRuntimeSetup.NeedsInstall(["Microsoft.AspNetCore.App/10.0.2"]),
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
