using Domain.Frontol.Enums;

namespace Domain.Frontol.Models.Settings;

public record DocumentKindTemplate
{
    public int Code { get; set; }

    public int TemplateCode { get; set; }

    public bool Preview { get; set; }

    public int CopyCount { get; set; }

    public DocOperationEnum Operation { get; set; }

    public DocOpeartionActEnum Act { get; set; }

    public bool UseCopyCount { get; set; }

    public bool PrintEmpty { get; set; }

    public bool DividePrintGroups { get; set; }

    public bool CancelOnErrors { get; set; }
}
