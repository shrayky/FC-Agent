using Domain.Frontol.Interfaces;
using FrontolDatabase;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolSchemaProviderMaintenanceTests
{
    [Test]
    public void Get_в_обслуживании_не_открывает_базу()
    {
        var maintenance = new ActiveMaintenance();
        var provider = new FrontolSchemaProvider(maintenance);

        var schema = provider.Get(
            "Server=localhost;Port=3050;Database=C:\\no-such.gdb;User=sysdba;Password=masterkey;Connection Timeout=1");

        Assert.That(schema.IsKnown, Is.False);
        Assert.That(schema.ReadError, Is.EqualTo(maintenance.Denial));
    }

    private sealed class ActiveMaintenance : IFrontolMaintenance
    {
        public bool IsActive => true;

        public DateTime? ResumeAtUtc => DateTime.UtcNow.AddHours(1);

        public string? Denial => "Сервисное обслуживание: база отключена до 23:31. Команды в базу не выполняются.";

        public void Enter(TimeSpan duration)
        {
        }

        public void Exit()
        {
        }
    }
}
