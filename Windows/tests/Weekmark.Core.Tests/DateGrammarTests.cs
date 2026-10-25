namespace Weekmark.Core.Tests;

public class DateGrammarTests
{
    static readonly DateOnly Today = new(2026, 10, 25); // Sunday

    static DateOnly? P(string s, bool dayFirst = true) => DateGrammar.Parse(s, Today, dayFirst);

    [Theory]
    [InlineData("tomorrow", 2026, 10, 26)]
    [InlineData("yesterday", 2026, 10, 24)]
    [InlineData("friday", 2026, 10, 30)]
    [InlineData("next friday", 2026, 10, 30)]
    [InlineData("last friday", 2026, 10, 23)]
    [InlineData("sunday", 2026, 11, 1)]
    [InlineData("last sun", 2026, 10, 18)]
    [InlineData("Dec 12", 2026, 12, 12)]
    [InlineData("Jan 5", 2027, 1, 5)]
    [InlineData("25 Oct", 2026, 10, 25)]
    [InlineData("24th December.", 2026, 12, 24)]
    [InlineData("1st sept 2027", 2027, 9, 1)]
    [InlineData("24.12.26", 2026, 12, 24)]
    [InlineData("2026-12-24", 2026, 12, 24)]
    [InlineData("today", 2026, 10, 25)]
    public void RelativeAndYearlessRules(string s, int y, int m, int d) => Assert.Equal(new DateOnly(y, m, d), P(s));

    [Fact]
    public void NumericOrder()
    {
        Assert.Equal(new DateOnly(2027, 4, 3), P("03/04/2027", true));
        Assert.Equal(new DateOnly(2027, 3, 4), P("03/04/2027", false));
        foreach (var df in new[] { true, false })
        {
            Assert.Equal(new DateOnly(2026, 12, 24), P("24/12/2026", df));
            Assert.Equal(new DateOnly(2026, 12, 24), P("12/24/2026", df));
        }
    }

    [Theory]
    [InlineData("31 Feb 2027")] [InlineData("00/00/0000")] [InlineData("99/99/9999")] [InlineData("1 Jan 0")] [InlineData("0000-01-01")]
    [InlineData("12/12")] [InlineData("")] [InlineData("hello")] [InlineData("32 Jan")] [InlineData("Feb 29")] [InlineData("٣ Jan")]
    public void InvalidDatesAreNull(string s) => Assert.Null(P(s));
}
