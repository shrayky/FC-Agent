using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Frontol.Enums;

namespace FrontolDatabase.Entitys;

[Table("PAYMENT")]
public class Payment
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("CODE")]
    public int Code { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("OPERATION")]
    public PaymentOperationEnum Operation { get; set; }

    [Column("DELETED")]
    public int Deleted { get; set; }

    [Column("PRINTGROUPID")]
    public int? PrintGroupId { get; set; }

    [Column("ISFISCALPAYMENT")]
    public int? IsFiscalPayment { get; set; }

    [Column("FISCALOPERATION")]
    public int? FiscalOperation { get; set; }

    [Column("ECRPAYMENT")]
    public int? EcrPayment { get; set; }
}
