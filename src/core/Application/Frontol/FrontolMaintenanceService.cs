using Domain.Frontol;
using Domain.Frontol.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.DI.Attributes;

namespace Application.Frontol;

[AutoRegisterService(ServiceLifetime.Singleton)]
public sealed class FrontolMaintenanceService : IFrontolMaintenance
{
    private readonly IFrontolConnectionRelease _release;
    private readonly TimeProvider _time;
    private readonly ILogger<FrontolMaintenanceService> _logger;
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private int _generation;
    private DateTime? _resumeAtUtc;

    public FrontolMaintenanceService(IFrontolConnectionRelease release)
        : this(release, TimeProvider.System, NullLogger<FrontolMaintenanceService>.Instance)
    {
    }

    public FrontolMaintenanceService(IFrontolConnectionRelease release, TimeProvider time)
        : this(release, time, NullLogger<FrontolMaintenanceService>.Instance)
    {
    }

    public FrontolMaintenanceService(
        IFrontolConnectionRelease release,
        TimeProvider time,
        ILogger<FrontolMaintenanceService> logger)
    {
        _release = release;
        _time = time;
        _logger = logger;
    }

    public bool IsActive
    {
        get
        {
            lock (_gate)
                return _resumeAtUtc is not null;
        }
    }

    public DateTime? ResumeAtUtc
    {
        get
        {
            lock (_gate)
                return _resumeAtUtc;
        }
    }

    public string? Denial
    {
        get
        {
            lock (_gate)
            {
                return _resumeAtUtc is DateTime resumeAt
                    ? FrontolMaintenanceNotice.Format(resumeAt, _time.GetLocalNow().DateTime)
                    : null;
            }
        }
    }

    public void Enter(TimeSpan duration)
    {
        var min = TimeSpan.FromMinutes(FrontolMaintenanceLimits.MinMinutes);
        var max = TimeSpan.FromMinutes(FrontolMaintenanceLimits.MaxMinutes);
        if (duration < min || duration > max)
            throw new ArgumentOutOfRangeException(nameof(duration));

        ITimer? previous;
        string notice;

        // Флаг раньше сброса пула: новое открытие уже получит отказ,
        // а простаивающие вложения Firebird закроются следом.
        lock (_gate)
        {
            _generation++;
            var generation = _generation;
            _resumeAtUtc = _time.GetUtcNow().UtcDateTime + duration;
            notice = FrontolMaintenanceNotice.Format(_resumeAtUtc.Value, _time.GetLocalNow().DateTime);
            previous = _timer;
            _timer = _time.CreateTimer(
                _ => OnTimer(generation),
                null,
                duration,
                Timeout.InfiniteTimeSpan);
        }

        previous?.Dispose();
        _release.Release();
        _logger.LogInformation(notice);
    }

    public void Exit()
    {
        ITimer? timer;
        var wasActive = false;

        lock (_gate)
        {
            if (_resumeAtUtc is not null)
                wasActive = true;

            _generation++;
            _resumeAtUtc = null;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();

        if (wasActive)
            _logger.LogInformation("Сервисное обслуживание снято, база снова доступна");
    }

    private void OnTimer(int generation)
    {
        ITimer? timer = null;
        var expired = false;

        lock (_gate)
        {
            if (generation != _generation || _resumeAtUtc is null)
                return;

            _resumeAtUtc = null;
            timer = _timer;
            _timer = null;
            expired = true;
        }

        timer?.Dispose();

        if (expired)
            _logger.LogInformation("Сервисное обслуживание закончилось, база снова доступна");
    }
}
