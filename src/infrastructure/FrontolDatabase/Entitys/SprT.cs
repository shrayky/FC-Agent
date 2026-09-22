using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("SPRT")]
public class SprT
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("MARK")]
    public string Mark { get; set; } = string.Empty;

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("ISWARE")]
    public int IsWare { get; set; }

    [Column("PARENTID")]
    public int ParentId { get; set; }

    [Column("HIERLEVEL")]
    public int HierLevel { get; set; }

    [Column("FLAGS")]
    public int Flags { get; set; }

    [Column("TAXGROUPID")]
    public int TaxGroupId { get; set; }

    [Column("MEASURE")]
    public int Measure { get; set; }

    [Column("ITEMTYPE")]
    public int ItemType { get; set; }

    [Column("PRINTGROUPCLOSE")]
    public int PrintGroupClose { get; set; }

    [Column("DELETED")]
    public int Deleted { get; set; }

    [Column("WARETYPE")]
    public int WareType { get; set; }

    [Column("CHNG")]
    public long ChangeCount { get; set; }

    [Column("BDOCODE")]
    public int DatabaseCode { get; set; }

    [Column("OWNERBDO")]
    public int OwnerBdo { get; set; }

    [Column("INSCHNG")]
    public long InsertChange { get; set; }
}
