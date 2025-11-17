using Domain.Frontol.Enums;

namespace FrontolDatabase.Parsers;

public static class YesNoWareParsers
{
    public static YesNoWareEnum ParseYesNoWareEnum(string raw) => raw switch
        {
            "0" => YesNoWareEnum.Ware,
            "1" => YesNoWareEnum.Yes,
            _ => YesNoWareEnum.No
        };
    
    public static NoWareEnum ParseNoWareEnum(string raw) => raw switch
    {
        "0" => NoWareEnum.Ware,
        _ => NoWareEnum.No
    };

    public static string MapYesNoWareToDb(YesNoWareEnum value) => value switch
    {
        YesNoWareEnum.Ware => "0",
        YesNoWareEnum.Yes => "1",
        _ => "2"
    };

    public static string MapNoWareToDb(NoWareEnum value) => value switch
    {
        NoWareEnum.Ware => "0",
        _ => "1",
    };
}