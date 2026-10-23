using Domain.Frontol.Enums;

namespace Domain.Frontol.Models.Settings;

public record DocumentTemplate
{
    public int Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFolder { get; set; }

    public int ParentCode { get; set; }

    public int MinLines { get; set; }

    public int MaxLines { get; set; }

    public string TemplateType { get; set; } = string.Empty;

    public PrintTargetEnum PrintTarget { get; set; }

    public int DocOrientation { get; set; }

    public string ScriptData { get; set; } = string.Empty;

    public byte[] FastReport { get; set; } = [];

    public string MasterSettings { get; set; } = string.Empty;
}
