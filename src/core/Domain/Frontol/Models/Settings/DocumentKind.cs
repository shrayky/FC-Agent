using Domain.Frontol.Enums;

namespace Domain.Frontol.Models.Settings;

public record DocumentKind
{
    public int Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Identif { get; set; } = string.Empty;

    public ReceiptTypeEnum ReceiptType { get; set; }

    public bool AskComment { get; set; }

    public bool AskEmployee { get; set; }

    public bool CopyEmployee { get; set; }

    public bool AskCommentOnOpen { get; set; }

    public bool Recompense { get; set; }

    // null — строки этого вида не меняем. Пустой список удаляет строки, которых в нём нет.
    public List<DocumentKindTemplate>? DocumentKindTemplates { get; set; }
}
