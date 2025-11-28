using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("LOG")]
public class LogDb
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }
    
    [Column("DATETIME")]
    public DateTime Date { get; set; }
    
    [Column("USERCODE")]
    public int UserCode { get; set; }
    
    [Column("STATE")]
    public int State { get; set; }
    
    [Column("FUNCID")]
    public int FuncId { get; set; }
    
    [Column("CATEG")]
    public string Category { get; set; } = string.Empty;
    
    [Column("POSNUMBER")]
    public int PosNumber { get; set; }
    
    [Column("DOCNUMBER")]
    public string DocNumber { get; set; } = string.Empty;
    
    [Column("ACTION")]
    public string Action { get; set; } = string.Empty;
}