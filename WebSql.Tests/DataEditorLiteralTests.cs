using WebSql.Shared;

namespace WebSql.Tests;

[TestClass]
public class FormatLiteralTests
{
    [TestMethod]
    [DataRow("42", "int", "42")]
    [DataRow(" -3.5 ", "decimal", "-3.5")]
    [DataRow("1e5", "float", "1e5")]
    [DataRow("True", "bit", "1")]
    [DataRow("false", "bit", "0")]
    public void Numbers_AreValidatedAndPassedThrough(string value, string type, string expected)
    {
        Assert.AreEqual(expected, DatabaseEngine.SqlServer.FormatLiteral(value, type));
    }

    [TestMethod]
    [DataRow("1; DROP TABLE users", "int")]
    [DataRow("1 OR 1=1", "bigint")]
    [DataRow("0x1F", "smallint")]
    [DataRow("abc", "decimal")]
    public void NonNumeric_InNumericColumn_IsRejected(string value, string type)
    {
        Assert.ThrowsException<FormatException>(() => DatabaseEngine.SqlServer.FormatLiteral(value, type));
    }

    [TestMethod]
    [DataRow("xml")]
    [DataRow("json")]
    [DataRow("enum")]
    [DataRow("uniqueidentifier")]
    [DataRow("varbinary")]
    [DataRow("something-new")]
    [DataRow(null)]
    public void EveryOtherType_IsQuoted_SoDataCanNeverBecomeSql(string? type)
    {
        Assert.AreEqual("'x''; DROP TABLE t; --'", DatabaseEngine.SqlServer.FormatLiteral("x'; DROP TABLE t; --", type));
    }

    [TestMethod]
    public void MySql_EscapesBackslashes()
    {
        // input:  a\' OR 1=1 --     output: the backslash doubled and the quote doubled
        Assert.AreEqual("'a\\\\'' OR 1=1 -- '", DatabaseEngine.MySql.FormatLiteral("a\\' OR 1=1 -- ", "enum"));
    }

    [TestMethod]
    public void Blank_IsNull()
    {
        Assert.AreEqual("NULL", DatabaseEngine.SqlServer.FormatLiteral("  ", "int"));
        Assert.AreEqual("NULL", DatabaseEngine.MySql.FormatLiteral(null, "varchar"));
    }
}
