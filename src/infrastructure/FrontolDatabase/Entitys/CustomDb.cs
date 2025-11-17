using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace FrontolDatabase.Entitys;

[Table("CUSTOMDB")]
public class CustomDb
{
    [Key]
    [Column("BDOCODE")]
    public int Code { get; set; }
    
    [Column("VERSIONFRONTOL")]
    public string VersionFrontol { get; set; } = string.Empty;
    
    [Column("ACTV")]
    public int Active { get; set; }
    
    [Column("DBIDENT")]
    public int DbIdent { get; set; }

    [Column("COPYLOGTOBDS")]
    public int? CopyLogToBds { get; set; }
    
    [Column("MAINTAINSTATE")]
    public int MaintainState { get; set; }
}