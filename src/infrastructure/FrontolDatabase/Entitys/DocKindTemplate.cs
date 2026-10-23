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
    public int? Code { get; set; }

    [Column("DOCKINDID")]
    public int? DocKindId { get; set; }

    [Column("DOCTEMPLID")]
    public int? DocTemplID { get; set; }

    [Column("PREVIEW")]
    public int? Preview { get; set; }

    [Column("COPYCOUNT")]
    public int? CopyCount { get; set; }

    [Column("OPERATION")]
    public DocOperationEnum? Operation { get; set; }

    [Column("ACT")]
    public DocOpeartionActEnum? Act { get; set; }

    [Column("USECOPYCOUNT")]
    public int? UseCopyCount { get; set; }

    [Column("PRINTEMPTY")]
    public int? PrintEmpty { get; set; }

    [Column("DIVIDEPRINTGROUPS")]
    public int? DividePrintGroups { get; set; }

    [Column("CANCELONERRORS")]
    public int? CancelOnErrors { get; set; }
}
