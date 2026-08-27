using Domain.Frontol.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("DOCKIND")]
public class DocKind
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("TEXT")]
    public string Text { get; set; } = string.Empty;

    [Column("IDENTIF")]
    public string DocumentTypeIdentificator { get; set; } = string.Empty;

    [Column("ECRRECEIPTTYPE")]
    public ReceiptTypeEnum ReceiptType { get; set; }

    [Column("ASKCOMMENT")]
    public bool AskComment { get; set; }

    [Column("ASKEMPLOYEE")]
    public bool AskEmployee { get; set; }

    [Column("COPYEMPLOYEE")]
    public bool CopyEmployee { get; set; }

    [Column("ASKCOMMENTONOPEN")]
    public bool AskCommentOnOpen { get; set; }

    [Column("RECOMPENSE")]
    public bool Recompense { get; set; }
}
