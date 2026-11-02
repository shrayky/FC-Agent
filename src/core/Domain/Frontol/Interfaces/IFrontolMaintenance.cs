namespace Domain.Frontol.Interfaces;

public interface IFrontolMaintenance
{
    bool IsActive { get; }

    DateTime? ResumeAtUtc { get; }

    string? Denial { get; }

    void Enter(TimeSpan duration);

    void Exit();
}
