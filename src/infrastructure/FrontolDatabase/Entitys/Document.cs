using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Frontol.Enums;

namespace FrontolDatabase.Entitys;

[Table("DOCUMENT")]
public class Document
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    // В базе Frontol жёстко заполнены ID, DOCKINDID, STATE, RMKID, OPENRMKID, OPENSESSION, ISFISCAL
    // (NOT NULL на уровне домена), а остальное Frontol пишет не всегда: у документов, созданных
    // интеграцией или операцией в ККМ, пустыми остаются LASTPAYMNUM, CLIENTID, DOCUMENTID и *USERID.
    // Поэтому для необязательных колонок здесь только nullable-типы: чтение NULL в int падает
    // на «Object cannot be cast from DBNull to other types» (см. SprT, Payment).
    [Column("DOCKINDID")]
    public int DocumentKindId { get; set; }

    [Column("CHEQUENUMBER")]
    public int? CheckNumber { get; set; }

    // OPENDATE/OPENTIME/CLOSEDATE/CLOSETIME Frontol пишет всегда: без них документа нет.
    [Column("OPENDATE")]
    public DateTime OpenDate { get; set; }

    [Column("OPENTIME")]
    public DateTime OpenTime { get; set; }

    [Column("OPENUSERID")]
    public int? OpenUserId { get; set; }

    [Column("CLOSEDATE")]
    public DateTime CloseDate { get; set; }

    [Column("CLOSETIME")]
    public DateTime CloseTime { get; set; }

    [Column("CLOSEUSERID")]
    public int? CloseUserId { get; set; }

    [Column("DOCUMENTID")]
    public long? BaseDocumentId { get; set; }

    [Column("CLIENTID")]
    public int? ClientId { get; set; }

    [Column("STATE")]
    public DocumentStateEnum State { get; set; }

    [Column("RMKID")]
    public int RmkId { get; set; }

    [Column("SUMM")]
    public double? Summ { get; set; }

    [Column("SUMMWD")]
    public double? SummWd { get; set; }

    [Column("ECRSESSION")]
    public int? EcrSession { get; set; }

    [Column("CHEQUETYPE")]
    public ReceiptTypeEnum? ChequeType { get; set; }

    [Column("ORDERIDENTIF")]
    public string? OrderIdentif { get; set; }

    [Column("OPENRMKID")]
    public int OpenRmkId { get; set; }

    [Column("OPENSESSION")]
    public int OpenSession { get; set; }

    [Column("ISFISCAL")]
    public int IsFiscal { get; set; }

    [Column("EXTID")]
    public string? ExtId { get; set; }

    [Column("PRINTGROUPCODE")]
    public int? PrintGroupCode { get; set; }

    [Column("LASTPAYMNUM")]
    public int? LastPaymNum { get; set; }

    [Column("OWNERUSERID")]
    public int? OwnerUserId { get; set; }

    [Column("UUID")]
    public string? Uuid { get; set; }
}
