using Domain.Frontol.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("DOCTEMPLATE")]
public class DocumentPrintFormTemplate
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("ISFOLDER")]
    public bool IsFolder { get; set; }

    [Column("PARENTID")]
    public int ParentID { get; set; }

    [Column("MINLINES")]
    public int MinLines { get; set; }

    [Column("MAXLINES")]
    public int MaxLines { get; set; }

    [Column("TEMPLATETYPE")]
    public string TemplateType { get; set; } = string.Empty;

    [Column("PRINTTARGET")]
    public PrintTargetEnum PrintTarget { get; set; }

    [Column("SCRIPTDATA")]
    public string ScriptData { get; set; } = string.Empty;

    [Column("DOCORIENTATION")]
    public int RotateOn180 { get; set; }

    [Column("FRDATA")]
    public byte[] FastReportDocument { get; set; } = [];

    [Column("MASTERSETTINGS")]
    public string MasterSettings { get; set; } = string.Empty;
}
