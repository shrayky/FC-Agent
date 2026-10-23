namespace Domain.Frontol.Models.Settings;

public record DocumentConfiguration
{
    public List<DocumentKind>? DocumentKinds { get; init; }

    public List<DocumentTemplate>? DocumentTemplates { get; init; }

    public List<DocumentPrintGroup>? PrintGroups { get; init; }

    public List<DocumentComment>? Comments { get; init; }
}
