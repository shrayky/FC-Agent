using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Receipts;

namespace Domain.Frontol.Interfaces;

public interface IFrontolDeferredReceipts
{
    Task<Result<ReceiptList>> List();

    Task<Result<int>> Count();

    Task<Result<Receipt>> Cancel(long documentId);

    Task<Result<Receipt>> Close(long documentId, IReadOnlyList<ReceiptPaymentItem>? payments = null);

    Task<Result<Receipt>> AddPayment(long documentId, IReadOnlyList<ReceiptPaymentItem> payments);
}
