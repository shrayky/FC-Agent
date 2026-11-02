using System.Data.Common;
using Domain.Frontol.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FrontolDatabase;

public sealed class MaintenanceConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IFrontolMaintenance _maintenance;

    public MaintenanceConnectionInterceptor(IFrontolMaintenance maintenance)
    {
        _maintenance = maintenance;
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        Refuse();
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        Refuse();
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    // Отказ до открытия: иначе клиент Firebird снова возьмёт вложение из пула.
    private void Refuse()
    {
        if (!_maintenance.IsActive)
            return;

        throw new InvalidOperationException(
            _maintenance.Denial ?? "Сервисное обслуживание: база отключена.");
    }
}
