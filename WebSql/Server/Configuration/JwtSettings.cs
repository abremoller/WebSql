namespace WebSql.Server.Configuration
{
    public class JwtSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "WebSql";
        public string Audience { get; set; } = "WebSqlClient";
        public int ExpirationMinutes { get; set; } = 1440; // 24 hours
    }
}
