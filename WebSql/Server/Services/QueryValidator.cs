using System.Text;
using System.Text.RegularExpressions;

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

        private static readonly Regex DeleteRx = new(@"\bDELETE\b", Opts);
        private static readonly Regex UpdateRx = new(@"\bUPDATE\b", Opts);
        private static readonly Regex WhereRx = new(@"\bWHERE\b", Opts);

        public QueryValidator(DangerousOperationsMode mode)
        {
            _mode = mode;
        }

        public QueryValidationResult Validate(string query, bool confirmedByUser = false)
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

            var (code, codeNoStrings) = Sanitize(query);
            var dangerousOps = new List<string>();

            foreach (var (label, pattern) in DangerousPatterns)
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
        /// Walks the SQL once, correctly handling '...' strings (with '' escapes), [bracketed] and "quoted"
        /// identifiers, -- line comments and nested /* block */ comments. Returns the query with comments
        /// removed (strings kept), and again with string / identifier contents blanked.
        /// </summary>
        public static (string Code, string CodeNoStrings) Sanitize(string sql)
        {
            var code = new StringBuilder(sql.Length);
            var bare = new StringBuilder(sql.Length);
            var i = 0;

            while (i < sql.Length)
            {
                var c = sql[i];

                if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    while (i < sql.Length && sql[i] != '\n') i++;
                    code.Append(' '); bare.Append(' ');
                }
                else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    var depth = 1;
                    i += 2;
                    while (i < sql.Length && depth > 0)
                    {
                        if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*') { depth++; i += 2; }
                        else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/') { depth--; i += 2; }
                        else i++;
                    }
                    code.Append(' '); bare.Append(' ');
                }
                else if (c == '\'' || c == '"' || c == '[')
                {
                    var close = c == '[' ? ']' : c;
                    var start = i;
                    i++;
                    while (i < sql.Length)
                    {
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
