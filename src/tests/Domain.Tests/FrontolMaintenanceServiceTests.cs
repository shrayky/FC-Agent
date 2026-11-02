using System.Threading;
using Application.Frontol;
using Domain.Frontol;
using Domain.Frontol.Interfaces;

namespace Domain.Tests;

[TestFixture]
public class FrontolMaintenanceServiceTests
{
    [Test]
    public void Enter_запрещает_базу_и_сбрасывает_соединения()
    {
        var release = new RecordingRelease();
        var service = new FrontolMaintenanceService(release, new ManualTime());

        service.Enter(TimeSpan.FromHours(1));

        var nowLocal = service.ResumeAtUtc!.Value.AddHours(-1).ToLocalTime();

        Assert.That(service.IsActive, Is.True);
        Assert.That(service.ResumeAtUtc, Is.EqualTo(DateTime.UtcNow.AddHours(1)).Within(TimeSpan.FromSeconds(2)));
        Assert.That(service.Denial, Is.EqualTo(FrontolMaintenanceNotice.Format(service.ResumeAtUtc.Value, nowLocal)));
        Assert.That(release.Calls, Is.EqualTo(1));
    }

    [Test]
    public void Exit_снова_разрешает_базу()
    {
        var service = new FrontolMaintenanceService(new RecordingRelease(), new ManualTime());
        service.Enter(TimeSpan.FromMinutes(FrontolMaintenanceLimits.MinMinutes));

        service.Exit();

        Assert.That(service.IsActive, Is.False);
        Assert.That(service.ResumeAtUtc, Is.Null);
        Assert.That(service.Denial, Is.Null);
    }

    [Test]
    public void Exit_без_режима_ничего_не_делает()
    {
        var service = new FrontolMaintenanceService(new RecordingRelease(), new ManualTime());

        service.Exit();

        Assert.That(service.IsActive, Is.False);
    }

    [Test]
    public void Повторный_Enter_сдвигает_срок_и_снова_сбрасывает_пул()
    {
        var release = new RecordingRelease();
        var service = new FrontolMaintenanceService(release, new ManualTime());
        service.Enter(TimeSpan.FromMinutes(15));

        service.Enter(TimeSpan.FromHours(1));

        Assert.That(service.ResumeAtUtc, Is.EqualTo(DateTime.UtcNow.AddHours(1)).Within(TimeSpan.FromSeconds(2)));
        Assert.That(release.Calls, Is.EqualTo(2));
    }

    [Test]
    public void Таймер_снимает_режим()
    {
        var time = new ManualTime();
        var service = new FrontolMaintenanceService(new RecordingRelease(), time);
        service.Enter(TimeSpan.FromHours(1));

        time.Fire();

        Assert.That(service.IsActive, Is.False);
        Assert.That(service.Denial, Is.Null);
    }

    [Test]
    public void Старый_таймер_не_снимает_новый_срок()
    {
        var time = new ManualTime();
        var service = new FrontolMaintenanceService(new RecordingRelease(), time);
        service.Enter(TimeSpan.FromMinutes(15));
        var first = time.Callback;

        service.Enter(TimeSpan.FromHours(1));
        first!(null);

        Assert.That(service.IsActive, Is.True);
    }

    [TestCase(0)]
    [TestCase(1441)]
    public void Срок_вне_диапазона_не_включает_режим(int minutes)
    {
        var release = new RecordingRelease();
        var service = new FrontolMaintenanceService(release, new ManualTime());

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Enter(TimeSpan.FromMinutes(minutes)));

        Assert.That(service.IsActive, Is.False);
        Assert.That(release.Calls, Is.EqualTo(0));
    }

    private sealed class RecordingRelease : IFrontolConnectionRelease
    {
        public int Calls { get; private set; }

        public void Release() => Calls++;
    }

    private sealed class ManualTime : TimeProvider
    {
        public TimerCallback? Callback { get; private set; }

        private object? _state;

        public void Fire() => Callback?.Invoke(_state);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Callback = callback;
            _state = state;
            return new IdleTimer();
        }

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
