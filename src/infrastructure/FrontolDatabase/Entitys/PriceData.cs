using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("PRICEDATA")]
public class PriceData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("ID")]
    public int Id { get; set; }

    [Column("PRICE")]
    public double Price { get; set; }

    [Column("ACTDATETIME")]
    public DateTime ActDateTime { get; set; }

    [Column("REMAINID")]
    public int RemainId { get; set; }
}
