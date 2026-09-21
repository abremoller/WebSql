using System.Text;
using System.Text.RegularExpressions;
using WebSql.Shared;

namespace WebSql.Server.Services
{
    public enum DangerousOperationsMode
    {
        Disabled,  // Block dangerous operations
        Prompt,    // Require confirmation
        Enabled    // Allow without confirmation
    }

    /// <summary>
    /// A safety net against accidents (a forgotten WHERE, a stray DROP). It is NOT a security boundary:
    /// dynamic SQL can always be assembled in ways a scanner will miss. The real boundary is the
    /// permissions of the SQL login the user connects with - use a least-privilege login.
    /// </summary>
    public class QueryValidator
    {
        private readonly DangerousOperationsMode _mode;

        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Matched against the query with comments removed (string contents are KEPT, so a statement
        // hidden inside EXEC('...') is still seen; the cost is an occasional over-cautious prompt).
        private static readonly (string Label, Regex Pattern)[] DangerousPatterns =
        [
            ("DROP", new(@"\bDROP\s+(DATABASE|TABLE|VIEW|PROC(EDURE)?|FUNCTION|SCHEMA|INDEX|TRIGGER|LOGIN|USER|ROLE)\b", Opts)),
            ("TRUNCATE", new(@"\bTRUNCATE\b", Opts)),
            ("SHUTDOWN", new(@"\bSHUTDOWN\b", Opts)),
            ("DBCC", new(@"\bDBCC\b", Opts)),
            ("xp_ extended procedure", new(@"\bxp_\w+", Opts)),
            ("sp_configure", new(@"\bsp_configure\b", Opts)),
            ("BACKUP", new(@"\bBACKUP\b", Opts)),
            ("RESTORE", new(@"\bRESTORE\b", Opts)),
            ("EXEC / dynamic SQL", new(@"\b(EXEC|EXECUTE|sp_executesql)\b", Opts)),
            ("OPENROWSET/OPENDATASOURCE/OPENQUERY", new(@"\b(OPENROWSET|OPENDATASOURCE|OPENQUERY)\b", Opts)),
            ("BULK INSERT", new(@"\bBULK\s+INSERT\b", Opts)),
            ("ALTER DATABASE/LOGIN/SERVER", new(@"\bALTER\s+(DATABASE|LOGIN|SERVER|AUTHORIZATION)\b", Opts)),
            ("CREATE LOGIN", new(@"\bCREATE\s+LOGIN\b", Opts)),
            ("GRANT/REVOKE/DENY", new(@"\b(GRANT|REVOKE|DENY)\b", Opts)),
        ];

        // MySQL / MariaDB equivalents. Same caveat: strings are kept so PREPARE ... FROM 'DROP ...' is still seen.
        private static readonly (string Label, Regex Pattern)[] MySqlDangerousPatterns =
        [
            ("DROP", new(@"\bDROP\s+(DATABASE|TABLE|VIEW|PROCEDURE|FUNCTION|SCHEMA|INDEX|TRIGGER|USER|ROLE|EVENT|SERVER)\b", Opts)),
            ("TRUNCATE", new(@"\bTRUNCATE\b", Opts)),
            ("SHUTDOWN", new(@"\bSHUTDOWN\b", Opts)),
            ("LOAD DATA", new(@"\bLOAD\s+(DATA|XML)\b", Opts)),
            ("INTO OUTFILE/DUMPFILE", new(@"\bINTO\s+(OUTFILE|DUMPFILE)\b", Opts)),
            ("LOAD_FILE", new(@"\bLOAD_FILE\s*\(", Opts)),
            ("SET GLOBAL/PASSWORD", new(@"\bSET\s+(GLOBAL|PERSIST|PERSIST_ONLY|PASSWORD)\b", Opts)),
            ("PREPARE / EXECUTE (dynamic SQL)", new(@"\b(PREPARE|EXECUTE)\b", Opts)),
            ("CREATE/ALTER/RENAME USER", new(@"\b(CREATE|ALTER|RENAME)\s+USER\b", Opts)),
            ("ALTER DATABASE/SERVER", new(@"\bALTER\s+(DATABASE|SCHEMA|SERVER)\b", Opts)),
            ("GRANT/REVOKE", new(@"\b(GRANT|REVOKE)\b", Opts)),
            ("INSTALL PLUGIN", new(@"\bINSTALL\s+(PLUGIN|COMPONENT)\b", Opts)),
            ("KILL / FLUSH / RESET", new(@"\b(KILL|FLUSH|RESET)\b", Opts)),
            ("CHANGE MASTER / REPLICATION", new(@"\b(CHANGE\s+(MASTER|REPLICATION)|START\s+(SLAVE|REPLICA)|STOP\s+(SLAVE|REPLICA))\b", Opts)),
        ];

        private static readonly Regex DeleteRx = new(@"\bDELETE\b", Opts);
        private static readonly Regex UpdateRx = new(@"\bUPDATE\b", Opts);
        private static readonly Regex WhereRx = new(@"\bWHERE\b", Opts);

        public QueryValidator(DangerousOperationsMode mode)
        {
            _mode = mode;
        }

        public QueryValidationResult Validate(string query, bool confirmedByUser = false, DatabaseEngine engine = DatabaseEngine.SqlServer)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new QueryValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Query cannot be empty",
                    Severity = ValidationSeverity.Error
                };
            }

            var (code, codeNoStrings) = Sanitize(query, engine);
            var dangerousOps = new List<string>();

            var patterns = engine == DatabaseEngine.MySql ? MySqlDangerousPatterns : DangerousPatterns;
            foreach (var (label, pattern) in patterns)
                if (pattern.IsMatch(code))
                    dangerousOps.Add(label);

            // UPDATE / DELETE with no WHERE - judged per statement (a WHERE in a *different* statement
            // does not count) and ignoring text inside string literals ('...where...' does not count).
            var statements = codeNoStrings.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var statement in statements)
            {
                if (WhereRx.IsMatch(statement)) continue;
                if (DeleteRx.IsMatch(statement)) dangerousOps.Add("DELETE without WHERE");
                if (UpdateRx.IsMatch(statement)) dangerousOps.Add("UPDATE without WHERE");
            }

            dangerousOps = dangerousOps.Distinct().ToList();

            if (dangerousOps.Any())
            {
                switch (_mode)
                {
                    case DangerousOperationsMode.Disabled:
                        return new QueryValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"Dangerous operation(s) detected: {string.Join(", ", dangerousOps)}. These operations are not allowed.",
                            Severity = ValidationSeverity.Error,
                            DangerousOperation = dangerousOps.First(),
                            RequiresConfirmation = false
                        };

                    case DangerousOperationsMode.Prompt:
                        if (!confirmedByUser)
                        {
                            return new QueryValidationResult
                            {
                                IsValid = false,
                                ErrorMessage = $"⚠️ DANGEROUS OPERATION: {string.Join(", ", dangerousOps)}\n\nThis operation could result in data loss or system changes. Are you sure you want to proceed?",
                                Severity = ValidationSeverity.Warning,
                                DangerousOperation = dangerousOps.First(),
                                RequiresConfirmation = true
                            };
                        }
                        break; // user confirmed

                    case DangerousOperationsMode.Enabled:
                        break;
                }
            }

            if (statements.Length > 1)
            {
                return new QueryValidationResult
                {
                    IsValid = true,
                    WarningMessage = $"Query contains {statements.Length} statements. Be cautious with batch queries.",
                    Severity = ValidationSeverity.Warning
                };
            }

            return new QueryValidationResult
            {
                IsValid = true,
                Severity = ValidationSeverity.None
            };
        }

        /// <summary>
        /// Walks the SQL once, correctly handling '...' strings (with '' escapes), quoted identifiers
        /// ([bracketed] and "quoted" for SQL Server, `backticked` for MySQL), line comments and block comments.
        /// Returns the query with comments removed (strings kept), and again with string / identifier
        /// contents blanked.
        /// </summary>
        /// <remarks>
        /// SQL Server: block comments nest; [x] is an identifier.
        /// MySQL: '#' starts a line comment, '--' only when followed by whitespace, block comments do not
        /// nest, backslash escapes inside strings, and /*! ... */ is executable code, not a comment.
        /// </remarks>
        public static (string Code, string CodeNoStrings) Sanitize(string sql, DatabaseEngine engine = DatabaseEngine.SqlServer)
        {
            var mysql = engine == DatabaseEngine.MySql;
            var code = new StringBuilder(sql.Length);
            var bare = new StringBuilder(sql.Length);
            var i = 0;

            while (i < sql.Length)
            {
                var c = sql[i];

                if ((c == '-' && i + 1 < sql.Length && sql[i + 1] == '-' && (!mysql || i + 2 >= sql.Length || char.IsWhiteSpace(sql[i + 2])))
                    || (mysql && c == '#'))
                {
                    while (i < sql.Length && sql[i] != '\n') i++;
                    code.Append(' '); bare.Append(' ');
                }
                else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    if (mysql && i + 2 < sql.Length && sql[i + 2] == '!')
                    {
                        // Executable comment: the server runs what's inside, so keep it as code.
                        var close = sql.IndexOf("*/", i + 3, StringComparison.Ordinal);
                        var end = close < 0 ? sql.Length : close;
                        code.Append(' ').Append(sql, i + 3, end - (i + 3)).Append(' ');
                        bare.Append(' ').Append(sql, i + 3, end - (i + 3)).Append(' ');
                        i = close < 0 ? sql.Length : close + 2;
                        continue;
                    }

                    var depth = 1;
                    i += 2;
                    while (i < sql.Length && depth > 0)
                    {
                        if (!mysql && sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*') { depth++; i += 2; }
                        else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/') { depth--; i += 2; }
                        else i++;
                    }
                    code.Append(' '); bare.Append(' ');
                }
                else if (c == '\'' || c == '"' || (mysql ? c == '`' : c == '['))
                {
                    var close = c == '[' ? ']' : c;
                    var backslashEscapes = mysql && c != '`';
                    var start = i;
                    i++;
                    while (i < sql.Length)
                    {
                        if (backslashEscapes && sql[i] == '\\') { i += 2; continue; } // \' does not end the string
                        if (sql[i] == close)
                        {
                            if (i + 1 < sql.Length && sql[i + 1] == close) { i += 2; continue; } // doubled = escaped
                            break;
                        }
                        i++;
                    }
                    var end = Math.Min(i + 1, sql.Length);
                    code.Append(sql, start, end - start);
                    bare.Append(c).Append(close);
                    i = end;
                }
                else
                {
                    code.Append(c); bare.Append(c);
                    i++;
                }
            }

            return (code.ToString(), bare.ToString());
        }
    }

    public class QueryValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string? DangerousOperation { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    public enum ValidationSeverity
    {
        None,
        Warning,
        Error
    }
}
