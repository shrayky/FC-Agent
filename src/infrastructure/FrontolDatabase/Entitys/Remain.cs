using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("REMAIN")]
public class Remain
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("ID")]
    public int Id { get; set; }

    [Column("WAREID")]
    public int WareId { get; set; }

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

    [Column("DELETED")]
    public int Deleted { get; set; }

    [Column("CHNG")]
    public long ChangeCount { get; set; }

    [Column("BDOCODE")]
    public int DatabaseCode { get; set; }

    [Column("USEREMAIN")]
    public int UseRemain { get; set; }

    [Column("OWNERBDO")]
    public int OwnerBdo { get; set; }

    [Column("INSCHNG")]
    public long InsertChange { get; set; }

    [Column("ENTERPRISEID")]
    public int EnterpriseId { get; set; }
}
