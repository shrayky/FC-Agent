using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("PROFILE")]
public class Profile
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }
    
    [Key]
    [Column("CODE")]
    public int Code { get; set; }
    
    [Column("NAME")]
    public string Name { get; set; } = string.Empty;
    
    [Column("SKIPMODE")]
    public bool SkipSupervisorMode { get; set; }
    
    [Column("FRONTOLSEILFIE")]
    public bool ForSelfieUser { get; set; }
    
    [Column("BASE")]
    public bool DontChangeUsersOnExchange { get; set; }
}