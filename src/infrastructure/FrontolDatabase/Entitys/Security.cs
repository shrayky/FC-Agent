using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("SECURITY")]
public class Security
{
    [Key]
    [Column("PROFILEID")]
    public int ProfileId { get; set; }
    
    [Column("SECURITY")]
    public int SecurityCode {get; set;}
    
    [Column("SECURITYVALUE")]
    public int Value {get; set;}
}