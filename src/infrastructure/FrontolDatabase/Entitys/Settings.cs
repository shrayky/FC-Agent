using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("SETTINGS")]
public class Settings
{
    [Column("ID")]
    [Key]
    public int Id { get; set; }
    
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;
    
    [Column("VAL")]
    public string Value { get; set; } = string.Empty;

    [Column("SYNCCATEG")]
    public int SyncCategory { get; set; } = 0;

    [Column("OWNERBDO")]
    public int OwnerBdo { get; set; } = 0;
}