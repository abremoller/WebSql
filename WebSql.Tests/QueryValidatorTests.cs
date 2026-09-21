using WebSql.Server.Services;
using WebSql.Shared;

namespace WebSql.Tests;

[TestClass]
public class QueryValidatorTests
{
    private static QueryValidationResult Prompt(string sql, bool confirmed = false) =>
        new QueryValidator(DangerousOperationsMode.Prompt).Validate(sql, confirmed);

    [TestMethod]
    [DataRow("SELECT * FROM Customers")]
    [DataRow("select top 10 name from sys.databases order by name")]
    [DataRow("UPDATE Customers SET Name = 'x' WHERE Id = 4")]
    [DataRow("DELETE FROM Customers WHERE Id = 4")]
    [DataRow("SELECT 'you can DROP TABLE users' AS note")] // inside a string in a SELECT is still cautious? see below
    public void SafeStatements_AreNotFlagged_ExceptStringCautionCase(string sql)
    {
        var r = Prompt(sql);
        // The last row deliberately contains the text DROP TABLE in a literal: the scanner keeps strings on purpose
        // (so EXEC('DROP TABLE x') is caught), so it prompts. Everything else must pass cleanly.
        if (sql.Contains("DROP TABLE"))
            Assert.IsTrue(r.RequiresConfirmation);
        else
            Assert.IsTrue(r.IsValid, r.ErrorMessage);
    }

    [TestMethod]
    [DataRow("DROP TABLE Customers")]
    [DataRow("drop   table Customers")]
    [DataRow("DROP/**/TABLE Customers")]                       // comment used to split the keyword
    [DataRow("DROP -- sneaky\nTABLE Customers")]
    [DataRow("SELECT '--'; DROP TABLE Customers")]             // comment marker inside a string must not swallow the rest
    [DataRow("SELECT 1; /* a /* nested */ comment */ DROP DATABASE Prod")]
    [DataRow("TRUNCATE TABLE Customers")]
    [DataRow("EXEC xp_cmdshell 'dir'")]
    [DataRow("exec xp_cmdshell 'dir'")]                        // lower case: the old validator never matched this
    [DataRow("EXEC sp_configure 'show advanced options', 1")]
    [DataRow("exec sp_configure 'x', 1")]
    [DataRow("EXEC('DR' + 'OP TABLE x')")]                     // dynamic SQL is always confirmed
    [DataRow("EXECUTE sp_executesql N'select 1'")]
    [DataRow("SELECT * FROM OPENROWSET('SQLNCLI','x','select 1')")]
    [DataRow("BACKUP DATABASE Prod TO DISK='x'")]
    [DataRow("GRANT CONTROL TO someone")]
    [DataRow("SHUTDOWN")]
    [DataRow("DBCC CHECKDB")]
    public void DangerousStatements_RequireConfirmation(string sql)
    {
        var r = Prompt(sql);
        Assert.IsFalse(r.IsValid, sql);
        Assert.IsTrue(r.RequiresConfirmation, sql);
    }

    [TestMethod]
    [DataRow("DELETE FROM Customers")]
    [DataRow("UPDATE Customers SET Name = 'x'")]
    [DataRow("UPDATE Customers SET Note = 'where'")]                     // the word WHERE inside a string literal doesn't count
    [DataRow("UPDATE Customers SET Name = 'x'; SELECT 1 WHERE 1 = 1")]  // a WHERE in another statement doesn't count
    [DataRow("DELETE FROM Customers -- WHERE Id = 1")]                   // ...nor one in a comment
    [DataRow("DELETE FROM Customers /* WHERE Id = 1 */")]
    [DataRow("UPDATE [where] SET a = 1")]                                // ...nor a bracketed identifier
    public void WriteWithoutWhere_IsFlagged(string sql)
    {
        var r = Prompt(sql);
        Assert.IsTrue(r.RequiresConfirmation, sql);
    }

    [TestMethod]
    public void Confirmed_InPromptMode_Executes()
    {
        Assert.IsTrue(Prompt("DROP TABLE Customers", confirmed: true).IsValid);
    }

    [TestMethod]
    public void DisabledMode_BlocksEvenWhenConfirmed()
    {
        var r = new QueryValidator(DangerousOperationsMode.Disabled).Validate("DROP TABLE Customers", confirmedByUser: true);
        Assert.IsFalse(r.IsValid);
        Assert.IsFalse(r.RequiresConfirmation);
    }

    [TestMethod]
    public void EnabledMode_AllowsWithoutPrompt()
    {
        Assert.IsTrue(new QueryValidator(DangerousOperationsMode.Enabled).Validate("DROP TABLE Customers").IsValid);
    }

    [TestMethod]
    public void EmptyQuery_IsInvalid()
    {
        Assert.IsFalse(Prompt("   ").IsValid);
    }

    [TestMethod]
    public void MultipleStatements_AreValidButWarned()
    {
        var r = Prompt("SELECT 1; SELECT 2");
        Assert.IsTrue(r.IsValid);
        Assert.IsNotNull(r.WarningMessage);
    }

    private static QueryValidationResult MySql(string sql, bool confirmed = false) =>
        new QueryValidator(DangerousOperationsMode.Prompt).Validate(sql, confirmed, DatabaseEngine.MySql);

    [TestMethod]
    [DataRow("SELECT * FROM `where` LIMIT 10")]
    [DataRow("UPDATE customers SET name = 'x' WHERE id = 4")]
    [DataRow("DELETE FROM customers WHERE id = 4")]
    [DataRow("SELECT `exec`, `backup` FROM t")]                 // SQL Server keywords are ordinary names in MySQL
    [DataRow("SHOW DATABASES")]
    public void MySql_SafeStatements_AreNotFlagged(string sql)
    {
        var r = MySql(sql);
        Assert.IsTrue(r.IsValid, r.ErrorMessage);
    }

    [TestMethod]
    [DataRow("DROP TABLE customers")]
    [DataRow("DROP/**/TABLE customers")]
    [DataRow("DROP # sneaky\nTABLE customers")]                 // '#' comment used to split the keyword
    [DataRow("DROP -- sneaky\nTABLE customers")]
    [DataRow("/*!50000 DROP TABLE customers */")]               // executable comment is real code in MySQL
    [DataRow(@"SELECT 'a\'; DROP TABLE customers; SELECT 'b'")] // backslash-escaped quote keeps the string open
    [DataRow("TRUNCATE TABLE customers")]
    [DataRow("SELECT * FROM t INTO OUTFILE '/tmp/x'")]
    [DataRow("LOAD DATA INFILE '/etc/passwd' INTO TABLE t")]
    [DataRow("SELECT LOAD_FILE('/etc/passwd')")]
    [DataRow("SET GLOBAL general_log = 1")]
    [DataRow("PREPARE s FROM 'DROP TABLE t'")]
    [DataRow("CREATE USER 'a'@'%' IDENTIFIED BY 'x'")]
    [DataRow("GRANT ALL ON *.* TO 'a'@'%'")]
    [DataRow("KILL 12")]
    public void MySql_DangerousStatements_RequireConfirmation(string sql)
    {
        var r = MySql(sql);
        Assert.IsFalse(r.IsValid, sql);
        Assert.IsTrue(r.RequiresConfirmation, sql);
    }

    [TestMethod]
    [DataRow("DELETE FROM customers")]
    [DataRow("UPDATE customers SET name = 'x'")]
    [DataRow("UPDATE customers SET note = 'where'")]
    [DataRow("DELETE FROM customers # WHERE id = 1")]           // a WHERE in a '#' comment doesn't count
    [DataRow("UPDATE `where` SET a = 1")]                       // ...nor a backticked identifier
    [DataRow(@"UPDATE t SET a = 'it\'s where'")]                // ...nor text after a backslash-escaped quote
    public void MySql_WriteWithoutWhere_IsFlagged(string sql)
    {
        Assert.IsTrue(MySql(sql).RequiresConfirmation, sql);
    }

    [TestMethod]
    public void MySql_DoubleDashWithoutSpace_IsNotAComment()
    {
        // In MySQL "1--1" is arithmetic, so the trailing text is still code and must still be scanned.
        Assert.IsTrue(MySql("SELECT 1--1; DROP TABLE t").RequiresConfirmation);
    }

    [TestMethod]
    public void MySql_BracketsAreNotIdentifierQuotes()
    {
        // "[" must not start a quoted region in MySQL, or a DROP after it would be hidden.
        var (code, _) = QueryValidator.Sanitize("SELECT a[1; DROP TABLE t; SELECT b]", DatabaseEngine.MySql);
        StringAssert.Contains(code, "DROP TABLE");
    }

    [TestMethod]
    public void Sanitize_MySql_UnterminatedInput_DoesNotThrow()
    {
        QueryValidator.Sanitize(@"SELECT 'oops\", DatabaseEngine.MySql);
        QueryValidator.Sanitize("SELECT `never closed", DatabaseEngine.MySql);
        QueryValidator.Sanitize("SELECT /*! never closed", DatabaseEngine.MySql);
        QueryValidator.Sanitize("SELECT #", DatabaseEngine.MySql);
        QueryValidator.Sanitize("SELECT --", DatabaseEngine.MySql);
    }

    [TestMethod]
    public void Sanitize_HandlesEscapedQuotes_AndUnterminatedInput_WithoutThrowing()
    {
        var (code, bare) = QueryValidator.Sanitize("SELECT 'it''s' -- tail");
        StringAssert.Contains(code, "'it''s'");
        Assert.IsFalse(bare.Contains("it"));

        // Unterminated string / comment / bracket must not throw or loop forever.
        QueryValidator.Sanitize("SELECT 'oops");
        QueryValidator.Sanitize("SELECT /* never closed");
        QueryValidator.Sanitize("SELECT [never closed");
    }
}
