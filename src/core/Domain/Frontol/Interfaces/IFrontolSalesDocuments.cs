using CSharpFunctionalExtensions;
using Domain.Messages.Dto;

namespace Domain.Frontol.Interfaces;

public interface IFrontolSalesDocuments
{
    Task<Result<IReadOnlyList<SalesDocument>>> After(long lastDocumentNumber, int limit);
}
