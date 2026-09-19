namespace WebSql.Server.Configuration
{
    public class JwtSettings
    {
        /// <summary>Leave empty: a random key is generated at startup (sessions don't survive restarts anyway).</summary>
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "WebSql";
        public string Audience { get; set; } = "WebSqlClient";
        public int ExpirationMinutes { get; set; } = 240;
    }
}
