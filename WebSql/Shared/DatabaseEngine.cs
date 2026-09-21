namespace WebSql.Shared
{
    public enum DatabaseEngine
    {
        SqlServer = 0,
        MySql = 1
    }

    /// <summary>Per-engine SQL syntax the client needs when it has to generate statements itself.</summary>
    public static class DatabaseEngineExtensions
    {
        public static string DisplayName(this DatabaseEngine engine) => engine switch
        {
            DatabaseEngine.MySql => "MySQL / MariaDB",
            _ => "SQL Server"
        };

        /// <summary>Quotes an identifier, doubling any embedded closing quote character.</summary>
        public static string QuoteIdentifier(this DatabaseEngine engine, string name) => engine switch
        {
            DatabaseEngine.MySql => "`" + name.Replace("`", "``") + "`",
            _ => "[" + name.Replace("]", "]]") + "]"
        };

        /// <summary>Quotes a table reference. MySQL has no schema level below the database, so a table is just db.table.</summary>
        public static string QuoteTable(this DatabaseEngine engine, string schema, string table) =>
            engine.QuoteIdentifier(schema) + "." + engine.QuoteIdentifier(table);

        public static string BeginTransactionSql(this DatabaseEngine engine) =>
            engine == DatabaseEngine.MySql ? "START TRANSACTION" : "BEGIN TRANSACTION";

        /// <summary>Escapes the inside of a single-quoted string literal.</summary>
        public static string EscapeStringLiteral(this DatabaseEngine engine, string value) =>
            engine == DatabaseEngine.MySql
                ? value.Replace("\\", "\\\\").Replace("'", "''")   // MySQL treats backslash as an escape by default
                : value.Replace("'", "''");
    }
}
