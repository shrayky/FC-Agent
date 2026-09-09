using Domain.Sales.Interfaces;

namespace Domain.Sales;

public class SalesCursorState : ISalesCursorState
{
    private readonly object _sync = new();

    public bool Initialized { get; private set; }
    public long LastDocumentNumber { get; private set; }

    public void Set(long documentNumber)
    {
        lock (_sync)
        {
            Initialized = true;
            LastDocumentNumber = documentNumber;
        }
    }
}
