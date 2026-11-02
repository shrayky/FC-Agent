using CentralServerExchange.Services;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models;
using Domain.Messages.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolStateServiceVersionTests
{
    [Test]
    public async Task Первая_и_та_же_версия_не_перезапускают_агент()
    {
        var harness = Create();
        harness.Versions.Enqueue(Result.Success("6.21.1"));
        harness.Versions.Enqueue(Result.Success("6.21.1"));

        await harness.Service.Current();
        await harness.Service.Current();

        Assert.That(harness.State.RestartSets, Is.EqualTo(0));
    }

    [Test]
    public async Task Другая_версия_ставит_флаг_перезапуска()
    {
        var harness = Create();
        harness.Versions.Enqueue(Result.Success("6.21.1"));
        harness.Versions.Enqueue(Result.Success("6.22.0"));

        await harness.Service.Current();
        await harness.Service.Current();

        Assert.That(harness.State.RestartSets, Is.EqualTo(1));
    }

    [Test]
    public async Task Пустой_и_неуспешный_ответ_не_стирают_запомненную_версию()
    {
        var harness = Create();
        harness.Versions.Enqueue(Result.Success("6.21.1"));
        harness.Versions.Enqueue(Result.Failure<string>("нет связи"));
        harness.Versions.Enqueue(Result.Success(string.Empty));
        harness.Versions.Enqueue(Result.Success("6.21.1"));

        await harness.Service.Current();
        await harness.Service.Current();
        await harness.Service.Current();
        await harness.Service.Current();

        Assert.That(harness.State.RestartSets, Is.EqualTo(0));
    }

    private static Harness Create()
    {
        var versions = new Queue<Result<string>>();
        var main = new Mock<IFrontolMainDb>();
        main.Setup(m => m.Version()).Returns(() => Task.FromResult(versions.Dequeue()));

        var receipts = new Mock<IFrontolDeferredReceipts>();
        receipts.Setup(m => m.Count()).ReturnsAsync(Result.Success(0));

        var services = new ServiceCollection();
        services.AddSingleton(main.Object);
        services.AddSingleton(receipts.Object);

        var parameters = new Mock<IParametersService>();
        parameters.Setup(m => m.Current()).ReturnsAsync(new Parameters
        {
            DatabaseConnection = new DatabaseConnection { DatabasePath = @"C:\no\main.gdb" },
            CentralServerSettings = new CentralServerConnection { Token = "token" }
        });

        var licenses = new Mock<IAtolLicenseSource>();
        licenses.Setup(m => m.Read(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AtolLicenseRead([], false));

        var maintenance = new Mock<IFrontolMaintenance>();
        maintenance.Setup(m => m.IsActive).Returns(false);

        var state = new RecordingState();
        var service = new FrontolStateService(
            NullLogger<FrontolStateService>.Instance,
            parameters.Object,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            licenses.Object,
            maintenance.Object,
            state);

        return new Harness(service, state, versions);
    }

    private sealed record Harness(FrontolStateService Service, RecordingState State, Queue<Result<string>> Versions);

    private sealed class RecordingState : IApplicationState
    {
        public int RestartSets { get; private set; }

        public void DbStateUpdate(bool isOnline)
        {
        }

        public bool DbState() => false;

        public void UpdateNeedRestart(bool need)
        {
            if (need)
                RestartSets++;
        }

        public bool NeedRestart() => RestartSets > 0;

        public void NewVersionInformationUpdate(NewVersionResponse value)
        {
        }

        public NewVersionResponse NewVersionInformation() => new();
    }
}
