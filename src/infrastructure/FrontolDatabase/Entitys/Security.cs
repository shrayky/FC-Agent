using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("SECURITY")]
public class Security
{
    [Key]
    [Column("PROGILEID")]
    public int ProfileId { get; set; }
    
    [Column("SECURITU")]
    public int SecurityCode {get; set;}
    
    [Column("SECURITUVALUE")]
    public int Value {get; set;}
}