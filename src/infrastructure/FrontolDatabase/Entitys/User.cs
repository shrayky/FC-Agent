using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("USER")]
public class User
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("PROFILEID")]
    public int ProfileId { get; set; }

    [Column("PSWRD")]
    public string Pswrd { get; set; } = string.Empty;

    [Column("CARD")]
    public string Card { get; set; } = string.Empty;

    [Column("DELETED")]
    public int Deleted { get; set; }

    [Column("BDOCODE")]
    public int BdoCode { get; set; }

    [Column("CHNG")]
    public long ChangeCount { get; set; }
}
