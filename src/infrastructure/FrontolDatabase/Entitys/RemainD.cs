using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("REMAIND")]
public class RemainD
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("ID")]
    public int Id { get; set; }

    [Column("REMAINID")]
    public int RemainId { get; set; }

    [Column("DELTA")]
    public double Delta { get; set; }

    [Column("DOCUMENTID")]
    public long DocumentId { get; set; }

    [Column("CHNG")]
    public long ChangeCount { get; set; }

    [Column("BDOCODE")]
    public int DatabaseCode { get; set; }

    [Column("DTYPE")]
    public int DType { get; set; }

    [Column("OWNERBDO")]
    public int OwnerBdo { get; set; }
}
