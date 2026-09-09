namespace Domain.Sales.Interfaces;

public interface ISalesCursorState
{
    bool Initialized { get; }
    long LastDocumentNumber { get; }
    void Set(long documentNumber);
}
