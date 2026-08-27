using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Frontol.Enums;

namespace FrontolDatabase.Entitys;

[Table("Devices")]
public class Devices
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("TYPEDEV")]
    public DeviceTypeEnum DeviceType { get; set; } = 0;

    [Column("BUSY")]
    public bool KeepConnetion { get; set; }

    [Column("ISFOLDER")]
    public bool IsFolder { get; set; }

    [Column("PARENTID")]
    public int ParentID { get; set; }

    [Column("TEXT")]
    public string Text { get; set; } = string.Empty;

    [Column("CONNECTIONSTRING")]
    public string Settings {  get; set; } = string.Empty;

    [Column("CONNECTIONSTATE")]
    public DeviceConnectionStateEnum ConnectionState { get; set; }
}
