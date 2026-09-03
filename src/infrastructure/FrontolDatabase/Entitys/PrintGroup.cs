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
}
