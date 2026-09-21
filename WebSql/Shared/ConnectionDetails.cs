namespace WebSql.Shared
{
    public class ConnectionDetails
    {
        public DatabaseEngine Engine { get; set; } = DatabaseEngine.SqlServer;
        public string ServerName { get; set; } = string.Empty;
        public string? Login { get; set; }
        public string? Password { get; set; }
        public bool IntegratedSecurity { get; set; }
        public string? SelectedDatabase { get; set; }
    }
}
