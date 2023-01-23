using GedcomParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GedcomTests
{
    [TestClass]
    public class DateValueTests
    {
        [DataTestMethod]
        [DataRow("3/21/1988", "1988-03-21")]
        [DataRow("03 Nov 1952", "1952-11-03")]
        [DataRow("03 Nov 1952 13:42", "1952-11-03T13:42")]
        [DataRow("03 Nov 1952 13:42:16.2", "1952-11-03T13:42:16")]
        [DataRow("03 Nov 1952 13:42:16Z", "1952-11-03T13:42:16")]
        [DataRow("03 Nov 1952 13:42:16.2Z", "1952-11-03T13:42:16")]
        [DataRow("1950", "1950")]
        [DataRow("7 Feb 1932", "1932-02-07")]
        [DataRow("Abt 1968", "1968~")]
        [DataRow("23 January 1984", "1984-01-23")]
        [DataRow("December 26, 1937", "1937-12-26")]
        [DataRow("December 1937", "1937-12")]
        [DataRow("Dec 1937", "1937-12")]
        [DataRow("1937-XX-03", "1937-XX-03")]
        // https://www.loc.gov/standards/datetime/
        [DataRow("1985-04-12", "1985-04-12")]
        [DataRow("1985-04", "1985-04")]
        [DataRow("1985", "1985")]
        [DataRow("1985-04-12T23:20:30", "1985-04-12T23:20:30")]
        [DataRow("1985-04-12T23:20:30Z", "1985-04-12T23:20:30")]
        [DataRow("1985-04-12T23:20:30-04", "1985-04-12T23:20:30")]
        [DataRow("1985-04-12T23:20:30+04:30", "1985-04-12T23:20:30")]
        [DataRow("Y30000", "Y30000")]
        [DataRow("Y-30000", "Y-30000")]
        [DataRow("1984?", "1984~")]
        [DataRow("2004-06~", "2004-06~")]
        [DataRow("2004-06-11%", "2004-06-11~")]
        [DataRow("201X", "201X")]
        [DataRow("20XX", "20XX")]
        [DataRow("2004-XX", "2004")]
        [DataRow("1985-04-XX", "1985-04")]
        [DataRow("1985-XX-XX", "1985")]
        [DataRow("-1985", "-1985")]
        [DataRow("1900S2", "19XX")]
        [DataRow("1950S2", "1950S2")]
        [DataRow("156X-12-25", "156X-12-25")]
        [DataRow("15XX-12-25", "15XX-12-25")]
        public void DateValueParse(string original, string expected)
        {
            var value = ExtendedDateTime.Parse(original);
            Assert.AreEqual(expected, value.ToString("s"));
        }

        [DataTestMethod]
        [DataRow("3/21/1988", "1988-03-21")]
        [DataRow("03 Nov 1952", "1952-11-03")]
        [DataRow("1950", "1950")]
        [DataRow("7 Feb 1932", "1932-02-07")]
        [DataRow("Before 1951", "../1951")]
        [DataRow("Abt 1968", "1968~")]
        [DataRow("1964/2008", "1964/2008")]
        [DataRow("2004-06/2006-08", "2004-06/2006-08")]
        [DataRow("2004-02-01/2005-02-08", "2004-02-01/2005-02-08")]
        [DataRow("2004-02-01/2005-02", "2004-02-01/2005-02")]
        [DataRow("2004-02-01/2005", "2004-02-01/2005")]
        [DataRow("2005/2006-02", "2005/2006-02")]
        [DataRow("1985-04-12/..", "1985-04-12/..")]
        [DataRow("1985-04/..", "1985-04/..")]
        [DataRow("1985/..", "1985/..")]
        [DataRow("../1985-04-12", "../1985-04-12")]
        [DataRow("../1985-04", "../1985-04")]
        [DataRow("../1985", "../1985")]
        [DataRow("1985-04-12/", "1985-04-12/..")]
        [DataRow("1985-04/", "1985-04/..")]
        [DataRow("1985/", "1985/..")]
        [DataRow("/1985-04-12", "../1985-04-12")]
        [DataRow("/1985-04", "../1985-04")]
        [DataRow("/1985", "../1985")]
        [DataRow("2004-06-XX/2004-07-03", "2004-06/2004-07-03")]
        public void DateRangeParse(string original, string expected)
        {
            var value = ExtendedDateRange.Parse(original);
            Assert.AreEqual(expected, value.ToString("s"));
        }
    }
}
