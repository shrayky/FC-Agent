using CentralServerExchange.Services;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolSettingsServiceApplyTests
{
    [Test]
    public async Task Ошибка_документов_откатывает_базу_и_не_пишет_файлы()
    {
        var harness = Create();
        harness.Documents
            .Setup(m => m.Apply(It.IsAny<DocumentConfiguration>()))
            .ReturnsAsync(Result.Failure("документы"))
            .Callback(() => harness.Order.Add("documents"));

        var result = await harness.Service.ApplySettings(new FrontolSettings());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("документы"));
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "rollback"
        }));
    }

    [Test]
    public async Task Ошибка_файлов_после_фиксации_базы_не_перезапускает_Frontol()
    {
        var harness = Create();
        harness.Libraries
            .Setup(m => m.ToFiles(It.IsAny<IReadOnlyList<ScriptLibrary>>()))
            .ReturnsAsync(Result.Failure("файлы"))
            .Callback(() => harness.Order.Add("libraries"));

        var result = await harness.Service.ApplySettings(new FrontolSettings());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("файлы"));
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "commit", "libraries"
        }));
    }

    [Test]
    public async Task Успешный_пакет_фиксирует_базу_затем_файлы_и_только_потом_перезапуск()
    {
        var harness = Create();

        var result = await harness.Service.ApplySettings(new FrontolSettings());

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "commit",
            "libraries", "cash", "restart"
        }));
    }

    private static Harness Create()
    {
        var order = new List<string>();
        var main = new Mock<IFrontolMainDb>();
        var settings = new Mock<IFrontolSettings>();
        var profiles = new Mock<IFrontolUserProfiles>();
        var scripts = new Mock<IFrontolActionScripts>();
        var libraries = new Mock<IFrontolScriptLibraries>();
        var cash = new Mock<IFrontolCashRegisterDriverScripts>();
        var documents = new Mock<IFrontolDocumentConfiguration>();

        main.Setup(m => m.BeginWrite()).ReturnsAsync(Result.Success()).Callback(() => order.Add("begin"));
        main.Setup(m => m.CommitWrite()).ReturnsAsync(Result.Success()).Callback(() => order.Add("commit"));
        main.Setup(m => m.RollbackWrite()).Returns(Task.CompletedTask).Callback(() => order.Add("rollback"));
        main.Setup(m => m.Restart()).ReturnsAsync(Result.Success()).Callback(() => order.Add("restart"));

        settings.Setup(m => m.LoadGlobalControlConfig(It.IsAny<GlobalControl>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("global"));
        settings.Setup(m => m.LoadParameters(It.IsAny<List<FrontolParameter>>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("parameters"));
        profiles.Setup(m => m.LoadUserProfiles(It.IsAny<List<UserProfile>>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("profiles"));
        scripts.Setup(m => m.ToDb(It.IsAny<ActionScript>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("script"));
        libraries.Setup(m => m.ToFiles(It.IsAny<IReadOnlyList<ScriptLibrary>>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("libraries"));
        cash.Setup(m => m.ToFiles(It.IsAny<IReadOnlyList<AtolCashRegisterDriver10Srcipt>>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("cash"));
        documents.Setup(m => m.Apply(It.IsAny<DocumentConfiguration>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("documents"));

        var services = new ServiceCollection();
        services.AddSingleton(main.Object);
        services.AddSingleton(settings.Object);
        services.AddSingleton(profiles.Object);
        services.AddSingleton(scripts.Object);
        services.AddSingleton(libraries.Object);
        services.AddSingleton(cash.Object);
        services.AddSingleton(documents.Object);
        var provider = services.BuildServiceProvider();

        return new Harness(
            new FrontolSettingsService(
                new Mock<ILogger<FrontolSettingsService>>().Object,
                provider.GetRequiredService<IServiceScopeFactory>()),
            order,
            libraries,
            documents);
    }

    private sealed record Harness(
        FrontolSettingsService Service,
        List<string> Order,
        Mock<IFrontolScriptLibraries> Libraries,
        Mock<IFrontolDocumentConfiguration> Documents);
}
