using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolDocumentConfiguration
{
    Task<Result<DocumentConfiguration>> Read();

    Task<Result> Apply(DocumentConfiguration configuration);
}
