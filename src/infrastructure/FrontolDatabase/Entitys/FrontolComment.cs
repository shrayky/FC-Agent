using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("COMMENT")]
public class FrontolComment
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int? Code { get; set; }

    [Column("NAME")]
    public string? Name { get; set; }

    [Column("TEXT")]
    public string? Text { get; set; }

    [Column("DELETED")]
    public int? Deleted { get; set; }
}
