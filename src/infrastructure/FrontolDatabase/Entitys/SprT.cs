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

    [Column("DELETED")]
    public int Deleted { get; set; }

    [Column("WARETYPE")]
    public int WareType { get; set; }
}
