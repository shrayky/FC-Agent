using CentralServerExchange.Services;
using CSharpFunctionalExtensions;
using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolSettingsServiceApplyTests
{
    private const string ScriptText = "var x = 1;";

    [Test]
    public async Task Ошибка_документов_откатывает_базу_и_не_пишет_файлы()
    {
        var harness = Create();
        harness.Documents
            .Setup(m => m.Apply(It.IsAny<DocumentConfiguration>()))
            .ReturnsAsync(Result.Failure("документы"))
            .Callback(() => harness.Order.Add("documents"));

        var result = await harness.Service.ApplySettings(Packet(ScriptText));

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

        var result = await harness.Service.ApplySettings(Packet(ScriptText));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("файлы"));
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "layouts", "commit", "libraries"
        }));
    }

    [Test]
    public async Task Успешный_пакет_фиксирует_базу_затем_файлы_и_только_потом_перезапуск()
    {
        var harness = Create();

        var result = await harness.Service.ApplySettings(Packet(ScriptText));

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "layouts", "commit",
            "libraries", "cash", "restart"
        }));
    }

    [Test]
    public async Task Пустой_текст_стартового_скрипта_не_пишет_ACTSCRIPT_и_пишет_файлы()
    {
        var harness = Create();

        var result = await harness.Service.ApplySettings(Packet("   "));

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        harness.Scripts.Verify(m => m.ToDb(It.IsAny<ActionScript>()), Times.Never);
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "documents", "layouts", "commit",
            "libraries", "cash", "restart"
        }));
    }

    [Test]
    public async Task Тариф_Базовый_не_пишет_скрипты_и_перезапускает_Frontol()
    {
        var harness = Create(FrontolTariffState.ActiveBasic);

        var result = await harness.Service.ApplySettings(Packet(ScriptText));

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        harness.Scripts.Verify(m => m.ToDb(It.IsAny<ActionScript>()), Times.Never);
        harness.Libraries.Verify(m => m.ToFiles(It.IsAny<IReadOnlyList<ScriptLibrary>>()), Times.Never);
        harness.Cash.Verify(
            m => m.ToFiles(It.IsAny<IReadOnlyList<AtolCashRegisterDriver10Srcipt>>(), It.IsAny<bool>()),
            Times.Never);
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "documents", "layouts", "commit", "restart"
        }));
    }

    [Test]
    public async Task Непроверенный_тариф_и_непустой_текст_пишут_стартовый_скрипт()
    {
        var harness = Create(FrontolTariffState.Unverified);

        var result = await harness.Service.ApplySettings(Packet(ScriptText));

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        harness.Scripts.Verify(m => m.ToDb(It.IsAny<ActionScript>()), Times.Once);
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "layouts", "commit",
            "libraries", "cash", "restart"
        }));
    }

    [Test]
    public async Task Ошибка_макетов_откатывает_базу_и_не_пишет_файлы()
    {
        var harness = Create();
        harness.Layouts
            .Setup(m => m.Apply(It.IsAny<IReadOnlyList<TouchLayout>?>()))
            .ReturnsAsync(Result.Failure("макеты"))
            .Callback(() => harness.Order.Add("layouts"));

        var result = await harness.Service.ApplySettings(Packet(ScriptText));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("макеты"));
        Assert.That(harness.Order, Is.EqualTo(new[]
        {
            "begin", "global", "parameters", "profiles", "script", "documents", "layouts", "rollback"
        }));
        harness.Libraries.Verify(m => m.ToFiles(It.IsAny<IReadOnlyList<ScriptLibrary>>()), Times.Never);
    }

    [Test]
    public async Task Apply_передаёт_макеты_из_пакета()
    {
        var harness = Create();
        var packet = Packet(ScriptText);
        packet.UserInterface = new UserInterface
        {
            Layout = [new TouchLayout { Code = 4, Name = "Касса" }]
        };

        var result = await harness.Service.ApplySettings(packet);

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        harness.Layouts.Verify(
            m => m.Apply(It.Is<IReadOnlyList<TouchLayout>?>(rows => rows != null && rows.Single().Code == 4)),
            Times.Once);
    }

    [Test]
    public async Task Read_кладёт_макеты_в_UserInterface()
    {
        var harness = Create();

        var result = await harness.Service.ReadFrontolSettings();

        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : "");
        Assert.That(result.Value.UserInterface.Layout!.Single().Code, Is.EqualTo(4));
        Assert.That(result.Value.UserInterface.Layout!.Single().Data, Is.EqualTo(new byte[] { 9 }));
    }

    [Test]
    public async Task Ошибка_чтения_макетов_не_отдаёт_пакет()
    {
        var harness = Create();
        harness.Layouts
            .Setup(m => m.Read())
            .ReturnsAsync(Result.Failure<UserInterface>("макеты"));

        var result = await harness.Service.ReadFrontolSettings();

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("макеты"));
    }

    private static Harness Create(FrontolTariffState tariff = FrontolTariffState.NotBasic)
    {
        var order = new List<string>();
        var main = new Mock<IFrontolMainDb>();
        var settings = new Mock<IFrontolSettings>();
        var profiles = new Mock<IFrontolUserProfiles>();
        var scripts = new Mock<IFrontolActionScripts>();
        var libraries = new Mock<IFrontolScriptLibraries>();
        var cash = new Mock<IFrontolCashRegisterDriverScripts>();
        var documents = new Mock<IFrontolDocumentConfiguration>();
        var layouts = new Mock<IFrontolTouchLayouts>();
        var tariffs = new Mock<IFrontolTariff>();
        tariffs.Setup(m => m.Current(It.IsAny<CancellationToken>())).ReturnsAsync(tariff);

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
        layouts.Setup(m => m.Apply(It.IsAny<IReadOnlyList<TouchLayout>?>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => order.Add("layouts"));
        settings.Setup(m => m.GetGlobalControlConfig()).ReturnsAsync(Result.Success(new GlobalControl()));
        settings.Setup(m => m.GetParameters()).ReturnsAsync(Result.Success(new List<FrontolParameter>()));
        profiles.Setup(m => m.GetUserProfiles()).ReturnsAsync(Result.Success(new List<UserProfile>()));
        scripts.Setup(m => m.FromDb()).ReturnsAsync(Result.Success(new ActionScript()));
        libraries.Setup(m => m.FromFiles()).ReturnsAsync(Result.Success(new List<ScriptLibrary>()));
        cash.Setup(m => m.FromFiles()).ReturnsAsync(Result.Success(new List<AtolCashRegisterDriver10Srcipt>()));
        documents.Setup(m => m.Read()).ReturnsAsync(Result.Success(new DocumentConfiguration()));
        layouts.Setup(m => m.Read()).ReturnsAsync(Result.Success(new UserInterface
        {
            Layout = [new TouchLayout { Code = 4, Name = "Касса", Data = [9] }]
        }));

        var services = new ServiceCollection();
        services.AddSingleton(main.Object);
        services.AddSingleton(settings.Object);
        services.AddSingleton(profiles.Object);
        services.AddSingleton(scripts.Object);
        services.AddSingleton(libraries.Object);
        services.AddSingleton(cash.Object);
        services.AddSingleton(documents.Object);
        services.AddSingleton(layouts.Object);
        var provider = services.BuildServiceProvider();

        return new Harness(
            new FrontolSettingsService(
                new Mock<ILogger<FrontolSettingsService>>().Object,
                provider.GetRequiredService<IServiceScopeFactory>(),
                tariffs.Object,
                new Mock<IFrontolMaintenance>().Object),
            order,
            scripts,
            libraries,
            cash,
            documents,
            layouts);
    }

    private static FrontolSettings Packet(string text) => new()
    {
        Scripts = new FrontolAgentScripts
        {
            FrontolScript = new ActionScript { Text = text }
        }
    };

    private sealed record Harness(
        FrontolSettingsService Service,
        List<string> Order,
        Mock<IFrontolActionScripts> Scripts,
        Mock<IFrontolScriptLibraries> Libraries,
        Mock<IFrontolCashRegisterDriverScripts> Cash,
        Mock<IFrontolDocumentConfiguration> Documents,
        Mock<IFrontolTouchLayouts> Layouts);
}
