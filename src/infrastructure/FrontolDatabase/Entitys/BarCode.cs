using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("BARCODE")]
public class BarCode
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("ID")]
    public int Id { get; set; }

    [Column("WAREID")]
    public int WareId { get; set; }

    [Column("BARCODE")]
    public string Barcode { get; set; } = string.Empty;

    [Column("ASPECTVALUE1ID")]
    public int AspectValue1Id { get; set; }

    [Column("ASPECTVALUE2ID")]
    public int AspectValue2Id { get; set; }

    [Column("ASPECTVALUE3ID")]
    public int AspectValue3Id { get; set; }

    [Column("ASPECTVALUE4ID")]
    public int AspectValue4Id { get; set; }

    [Column("ASPECTVALUE5ID")]
    public int AspectValue5Id { get; set; }

    [Column("FACTOR")]
    public double Factor { get; set; }

    [Column("DELETED")]
    public int Deleted { get; set; }

    [Column("BDOCODE")]
    public int DatabaseCode { get; set; }

    [Column("CHNG")]
    public long ChangeCount { get; set; }

    [Column("OWNERBDO")]
    public int OwnerBdo { get; set; }

    [Column("INSCHNG")]
    public long InsertChange { get; set; }
}
