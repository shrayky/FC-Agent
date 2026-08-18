using FrontolDatabase.Parsers;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FrontolTimeParsersTests
{
    [TestCase("23:59", "30.12.1899 23:59:59:0")]
    [TestCase("00:00", "30.12.1899 0:0:0:0")]
    [TestCase("9:30", "30.12.1899 9:30:0:0")]
    [TestCase("08:05:10", "30.12.1899 8:5:10:0")]
    [TestCase("30.12.1899 23:59:59:0", "30.12.1899 23:59:59:0")]
    public void ToDb_ConvertsTimeOrDelphi(string input, string expected)
    {
        Assert.That(FrontolTimeParsers.ToDb(input), Is.EqualTo(expected));
    }

    [TestCase("1")]
    [TestCase("C:\\Frontol\\file.ini")]
    [TestCase("12:30 extra")]
    public void ToDb_LeavesNonTimeAsIs(string input)
    {
        Assert.That(FrontolTimeParsers.ToDb(input), Is.EqualTo(input));
    }

    [TestCase("30.12.1899 23:59:59:0", "23:59")]
    [TestCase("30.12.1899 0:0:0:0", "00:00")]
    [TestCase("30.12.1899 9:30:0:0", "09:30")]
    [TestCase("23:59", "23:59")]
    [TestCase("1", "1")]
    public void FromDb_ConvertsDelphiToHoursMinutes(string input, string expected)
    {
        Assert.That(FrontolTimeParsers.FromDb(input), Is.EqualTo(expected));
    }
}
