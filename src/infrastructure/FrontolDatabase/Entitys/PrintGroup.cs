using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("PRINTGROUP")]
public class PrintGroup
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("DELETED")]
    public int Deleted { get; set; }

    // Nullable: колонки есть не в каждой базе, а PRINTGROUP читают продажи и отложенные чеки.
    [Column("TEXT")]
    public string? Text { get; set; }

    [Column("PROPOSEFORCHEQUE")]
    public int? ProposeForCheque { get; set; }

    [Column("PROPOSEFORCOPY")]
    public int? ProposeForCopy { get; set; }

    [Column("PROPOSEFORPRECHEQUE")]
    public int? ProposeForPrecheque { get; set; }

    [Column("PRINTGROUPCLOSESESSIONREPORT")]
    public int? CloseSessionReport { get; set; }

    [Column("TAXSYSTEM")]
    public int? TaxSystem { get; set; }

    [Column("TAXGROUPID")]
    public int? TaxGroupId { get; set; }
}
