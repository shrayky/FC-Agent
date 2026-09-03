using CSharpFunctionalExtensions;
using Domain.Frontol.Models.DeferredReceipts;

namespace Domain.Frontol.Interfaces;

public interface IFrontolDeferredReceipts
{
    Task<Result<DeferredReceiptList>> List();

    Task<Result<DeferredReceipt>> Cancel(long documentId);

    Task<Result<DeferredReceipt>> Close(long documentId, IReadOnlyList<DeferredReceiptPaymentItem>? payments = null);

    Task<Result<DeferredReceipt>> AddPayment(long documentId, IReadOnlyList<DeferredReceiptPaymentItem> payments);
}
