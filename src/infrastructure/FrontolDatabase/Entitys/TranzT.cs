using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Frontol.Enums;

namespace FrontolDatabase.Entitys;

[Table("TRANZT")]
public class TranzT
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("ID")]
    public long Id { get; set; }

    [Column("DOCUMENTID")]
    public long DocumentId { get; set; }

    [Column("TRANZCOUNT")]
    public int? TranzCount { get; set; }

    [Column("TRANZDATE")]
    public DateTime TranzDate { get; set; }

    [Column("TRANZTIME")]
    public DateTime TranzTime { get; set; }

    [Column("TRANZHOUR")]
    public int TranzHour { get; set; }

    [Column("TRANZTYPE")]
    public TranzTypeEnum TranzType { get; set; }

    [Column("SELLER")]
    public int Seller { get; set; }

    [Column("WARECODE")]
    public int WareCode { get; set; }

    [Column("WAREMARK")]
    public string WareMark { get; set; } = string.Empty;

    [Column("PRICE")]
    public double Price { get; set; }

    [Column("QUANTITY")]
    public double Quantity { get; set; }

    [Column("SUMM")]
    public double Summ { get; set; }

    [Column("PRICEWD")]
    public double PriceWd { get; set; }

    [Column("SUMMWD")]
    public double SummWd { get; set; }

    [Column("INFO")]
    public int Info { get; set; }

    [Column("BARCODE")]
    public string Barcode { get; set; } = string.Empty;

    [Column("CURRENCY")]
    public int Currency { get; set; }

    [Column("POSID")]
    public int PosId { get; set; }

    [Column("POSNUMB")]
    public int PosNumb { get; set; }

    [Column("COMMENTCODE")]
    public int CommentCode { get; set; }

    [Column("COUNTFILLS")]
    public double CountFills { get; set; }

    [Column("ORDERPOS")]
    public int OrderPos { get; set; }

    [Column("TRMKID")]
    public int TrmkId { get; set; }

    [Column("PRINTGROUPCLOSE")]
    public int PrintGroupClose { get; set; }

    [Column("SUMMROUND")]
    public double SummRound { get; set; }

    [Column("DISCOUNTENABLED")]
    public int DiscountEnabled { get; set; }
}
