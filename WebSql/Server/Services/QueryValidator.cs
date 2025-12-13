using System.Text.RegularExpressions;

namespace WebSql.Server.Services
{
    public enum DangerousOperationsMode
    {
        Disabled,  // Block dangerous operations
        Prompt,    // Require confirmation
        Enabled    // Allow without confirmation
    }

    public class QueryValidator
    {
        private readonly DangerousOperationsMode _mode;

        private static readonly string[] DangerousKeywords = new[]
        {
            "DROP DATABASE",
            "DROP TABLE",
            "DROP VIEW", 
            "DROP PROCEDURE",
            "DROP FUNCTION",
            "TRUNCATE",
            "SHUTDOWN",
            "DBCC",
            "xp_cmdshell",
            "sp_configure",
            "BACKUP",
            "RESTORE"
        };

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

            var normalizedQuery = query.ToUpperInvariant().Trim();
            var dangerousOps = new List<string>();

            // Check for dangerous operations
            foreach (var keyword in DangerousKeywords)
            {
                if (normalizedQuery.Contains(keyword))
                {
                    dangerousOps.Add(keyword);
                }
            }

            // Check for DELETE without WHERE
            if (Regex.IsMatch(normalizedQuery, @"\bDELETE\s+FROM\b") && 
                !Regex.IsMatch(normalizedQuery, @"\bWHERE\b"))
            {
                dangerousOps.Add("DELETE without WHERE");
            }

            // Check for UPDATE without WHERE
            if (Regex.IsMatch(normalizedQuery, @"\bUPDATE\b") && 
                !Regex.IsMatch(normalizedQuery, @"\bWHERE\b"))
            {
                dangerousOps.Add("UPDATE without WHERE");
            }

            // Handle dangerous operations based on mode
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
                        // User confirmed, allow execution
                        break;

                    case DangerousOperationsMode.Enabled:
                        // Allow all operations without confirmation
                        break;
                }
            }

            // Warn about multiple statements (SQL injection risk)
            var statementCount = query.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
            if (statementCount > 1)
            {
                return new QueryValidationResult
                {
                    IsValid = true,
                    WarningMessage = $"Query contains {statementCount} statements. Be cautious with batch queries.",
                    Severity = ValidationSeverity.Warning
                };
            }

            return new QueryValidationResult
            {
                IsValid = true,
                Severity = ValidationSeverity.None
            };
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
