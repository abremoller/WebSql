namespace WebSql.Shared.DTOs
{
    public class ConnectRequest
    {
        public string ServerName { get; set; } = string.Empty;
        public string? Login { get; set; }
        public string? Password { get; set; }
        public bool IntegratedSecurity { get; set; }
    }

    public class ConnectResponse
    {
        public string SessionToken { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class QueryRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public bool ConfirmedDangerous { get; set; } = false;
    }

    public class QueryResponse
    {
        public Table? ResultTable { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsAffected { get; set; }
        public double ExecutionTimeMs { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    public class ObjectExplorerRequest
    {
        public string SessionToken { get; set; } = string.Empty;
    }

    public class ObjectExplorerResponse
    {
        public ObjectExplorer? Explorer { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DisconnectRequest
    {
        public string SessionToken { get; set; } = string.Empty;
    }

    public class ChangeDatabaseRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
