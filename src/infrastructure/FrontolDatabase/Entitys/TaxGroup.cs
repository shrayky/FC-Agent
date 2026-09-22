using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("TAXGROUP")]
public class TaxGroup
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
