using Domain.Frontol.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("DOCKINDTMPL")]
public class DocKindTemplate
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("DOCTEMPLID")]
    public int DocTemplID { get; set; }

    [Column("PREVIEW")]
    public bool Preview { get; set; }

    [Column("COPYCOUNT")]
    public int CopyCount { get; set; } = 0;

    [Column("OPERATION")]
    public DocOperationEnum Operation { get; set; }

    [Column("ACT")]
    public DocOpeartionActEnum Act { get; set; }

    [Column("USECOPYCOUNT")]
    public bool UseCopyCount { get; set; }

    [Column("PRINTEMPTY")]
    public bool PrintEmpty { get; set; }

    [Column("DIVIDEPRINTGROUPS")]
    public bool DividePrintGroups { get; set; }

    [Column("CANCELONERRORS")]
    public bool CancelOnErrors { get; set; }
}
