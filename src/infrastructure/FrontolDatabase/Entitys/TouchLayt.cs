using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("TOUCHLAYT")]
public class TouchLayt
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string? Name { get; set; }

    [Column("DATA")]
    public byte[]? Data { get; set; }
}
