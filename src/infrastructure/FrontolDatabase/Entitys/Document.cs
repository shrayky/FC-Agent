using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FrontolDatabase.Entitys;

[Table("DOCUMENTS")]
public class Document
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("DOCKINDID")]
    public int DocumentKindId { get; set; }

    [Column("CHEQUENUMBER")]
    public int CheckNumber { get; set; }

    [Column("OPENDATE")]
    public DateTime OpenDate { get; set; }

    [Column("OPENTIME")]
    public DateTime OpenTime {  get; set; }

    [Column("OPENUSERID")]
    public int OpenUserId { get; set; }

    [Column("CLOSEDATE")]
    public DateTime CloseDate {  get; set; }

    [Column("CLOSETIME")]
    public DateTime CloseTime { get; set; }

    //CloseUserID

    //DocumentID

    //ClientID

    //State

    //Education

    //RMKID

    //Summ

    //SummWD

    //ECRSession

    //ChequeType

    //OrderIdentif

    //CommentCode

    //AspectScheme

    //AspectValue1

    //AspectValue2

    //AspectValue3

    //AspectValue4

    //AspectValue5

    //HallPlaceID

    //Saved

    //InnerDocOrder

    //OpenRMKID


    //OpenSession

    //IsFiscal

    //NShop

    //UserValues

    //EmployeeCode

    //PrintGroupCode

    //ExtID

    //EnterpriseID
}
