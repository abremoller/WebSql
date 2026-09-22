namespace WebSql.Server.Security
{
    /// <summary>
    /// The single login that protects the whole site (config section "Auth").
    /// WebSql fails closed: until Username and PasswordHash are set, every request is refused.
    /// Generate a hash with:  dotnet run --project WebSql/Server -- hash-password
    /// </summary>
    public class AuthSettings
    {
        public string Username { get; set; } = string.Empty;

        /// <summary>PBKDF2 hash in the format produced by PasswordHasher.Hash.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Optional. If non-empty, only these client IP addresses may reach the site at all.</summary>
        public string[] AllowedIps { get; set; } = [];

        public int MaxFailedAttempts { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Username) && PasswordHasher.IsWellFormed(PasswordHash);
    }

    /// <summary>What connections and queries are allowed (config section "Security").</summary>
    public class SecuritySettings
    {
        /// <summary>Disabled = block, Prompt = ask the user to confirm, Enabled = allow. A guard against accidents, not a security boundary.</summary>
        public string DangerousOperationsMode { get; set; } = "Prompt";

        /// <summary>
        /// Allow "Integrated Security" (connect as the Windows account the web app runs under).
        /// Off by default: with it on, whoever can log in to WebSql inherits that account's SQL access.
        /// </summary>
        public bool AllowIntegratedSecurity { get; set; } = false;

        /// <summary>Skip SQL Server certificate validation. Off by default; only enable for servers with self-signed certs you trust.</summary>
        public bool TrustServerCertificate { get; set; } = false;

        /// <summary>Optional. If non-empty, users may only connect to these servers (matched case-insensitively).</summary>
        public string[] AllowedServers { get; set; } = [];

        /// <summary>A session that has not been used for this long is dropped (its credentials are forgotten).</summary>
        public double SessionIdleMinutes { get; set; } = 30;

        public int MaxSessions { get; set; } = 25;

        /// <summary>A query returning more rows than this is refused (protects the server's memory). 0 = no limit.</summary>
        public int MaxRows { get; set; } = 50_000;

        /// <summary>Per-query timeout.</summary>
        public int QueryTimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Addresses of reverse proxies (e.g. Plesk's nginx) whose X-Forwarded-For / X-Forwarded-Proto headers are believed,
        /// so lockout, rate limits and Auth:AllowedIps see the real client. Empty = headers are ignored (safe when not behind a proxy).
        /// </summary>
        public string[] TrustedProxies { get; set; } = [];
    }
}
