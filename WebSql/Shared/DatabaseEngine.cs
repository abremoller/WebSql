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

        private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "tinyint", "smallint", "mediumint", "int", "integer", "bigint",
            "decimal", "numeric", "dec", "fixed", "float", "real", "double", "double precision",
            "money", "smallmoney", "bit", "year"
        };

        private static readonly System.Text.RegularExpressions.Regex NumberRx =
            new(@"^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>
        /// Turns a value typed (or loaded) in the data editor into a SQL literal. Blank means NULL.
        /// Numeric types must really be numbers; EVERYTHING else is quoted, so a value in an unrecognised
        /// column type (xml, json, enum, uniqueidentifier ...) can never be interpreted as SQL.
        /// </summary>
        /// <exception cref="FormatException">The value is not valid for a numeric column.</exception>
        public static string FormatLiteral(this DatabaseEngine engine, string? value, string? dataType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NULL";

            if (dataType is not null && NumericTypes.Contains(dataType.Trim()))
            {
                var trimmed = value.Trim();
                if (NumberRx.IsMatch(trimmed))
                    return trimmed;

                // "True"/"False" is how a bit column comes back from the server.
                if (dataType.Trim().Equals("bit", StringComparison.OrdinalIgnoreCase))
                {
                    if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return "1";
                    if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return "0";
                }

                throw new FormatException($"'{(value.Length > 40 ? value[..40] + "..." : value)}' is not a valid {dataType} value.");
            }

            return "'" + engine.EscapeStringLiteral(value) + "'";
        }

        /// <summary>Escapes the inside of a single-quoted string literal.</summary>
        public static string EscapeStringLiteral(this DatabaseEngine engine, string value) =>
            engine == DatabaseEngine.MySql
                ? value.Replace("\\", "\\\\").Replace("'", "''")   // MySQL treats backslash as an escape by default
                : value.Replace("'", "''");
    }
}
