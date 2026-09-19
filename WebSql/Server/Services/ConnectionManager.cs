using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using WebSql.Server.Security;
using WebSql.Shared;

namespace WebSql.Server.Services
{
    /// <summary>
    /// Holds database sessions server-side (connection strings never go to the browser) and hands the
    /// browser a signed token that points at one. Sessions expire when idle.
    /// </summary>
    public class ConnectionManager : IConnectionManager
    {
        private readonly ConcurrentDictionary<string, ConnectionSession> _sessions = new();
        private readonly ILogger<ConnectionManager> _logger;
        private readonly IJwtService _jwtService;
        private readonly SecuritySettings _settings;

        public ConnectionManager(ILogger<ConnectionManager> logger, IJwtService jwtService, SecuritySettings settings)
        {
            _logger = logger;
            _jwtService = jwtService;
            _settings = settings;
        }

        public Task<string> CreateConnectionAsync(ConnectionDetails connectionDetails)
        {
            try
            {
                ValidateConnectionDetails(connectionDetails);
                RemoveExpiredSessions();

                if (_sessions.Count >= _settings.MaxSessions)
                    throw new InvalidOperationException("Too many open sessions. Disconnect one and try again.");

                string sessionId = Guid.NewGuid().ToString();
                var session = new ConnectionSession
                {
                    SessionToken = sessionId,
                    ConnectionString = BuildConnectionString(connectionDetails),
                    ConnectionDetails = connectionDetails,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };

                _sessions.TryAdd(sessionId, session);

                string jwtToken = _jwtService.GenerateToken(sessionId, connectionDetails.ServerName);

                _logger.LogInformation("Created new connection session: {SessionId} for server: {Server}",
                    sessionId, connectionDetails.ServerName);

                return Task.FromResult(jwtToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating connection session");
                throw;
            }
        }

        public string? GetConnectionString(string jwtToken)
        {
            var session = FindLiveSession(jwtToken);
            if (session is null) return null;

            session.LastAccessedAt = DateTime.UtcNow;
            return session.ConnectionString;
        }

        public Task DisconnectAsync(string jwtToken)
        {
            var sessionId = _jwtService.GetSessionIdFromToken(jwtToken);
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Invalid JWT token for disconnect");
                return Task.CompletedTask;
            }

            if (_sessions.TryRemove(sessionId, out _))
                _logger.LogInformation("Disconnected session: {SessionId}", sessionId);
            else
                _logger.LogWarning("Attempted to disconnect non-existent session: {SessionId}", sessionId);

            return Task.CompletedTask;
        }

        public bool ValidateSession(string jwtToken) => FindLiveSession(jwtToken) is not null;

        public void UpdateDatabase(string jwtToken, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || database.Length > 128)
                throw new ArgumentException("A valid database name is required");

            var session = FindLiveSession(jwtToken);
            if (session is null) return;

            var details = session.ConnectionDetails;
            details.SelectedDatabase = database;
            session.ConnectionString = BuildConnectionString(details);
            session.LastAccessedAt = DateTime.UtcNow;

            _logger.LogInformation("Updated database for session {SessionId} to {Database}", session.SessionToken, database);
        }

        // ---- policy ---------------------------------------------------------------------------

        private void ValidateConnectionDetails(ConnectionDetails details)
        {
            if (string.IsNullOrWhiteSpace(details.ServerName))
                throw new ArgumentException("Server name is required");

            if (details.ServerName.Length > 255 || details.ServerName.Any(char.IsControl))
                throw new ArgumentException("Server name is not valid");

            if (details.IntegratedSecurity && !_settings.AllowIntegratedSecurity)
                throw new InvalidOperationException(
                    "Integrated security is disabled on this server. Use a SQL login.");

            if (!details.IntegratedSecurity && string.IsNullOrWhiteSpace(details.Login))
                throw new ArgumentException("Login is required");

            if (_settings.AllowedServers.Length > 0 &&
                !_settings.AllowedServers.Any(s => string.Equals(s.Trim(), details.ServerName.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That server is not on this WebSql instance's allowed list.");
        }

        // ---- sessions -------------------------------------------------------------------------

        private ConnectionSession? FindLiveSession(string jwtToken)
        {
            if (string.IsNullOrWhiteSpace(jwtToken)) return null;

            var sessionId = _jwtService.GetSessionIdFromToken(jwtToken);
            if (string.IsNullOrEmpty(sessionId)) return null;

            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("Session not found: {SessionId}", sessionId);
                return null;
            }

            if (IsExpired(session))
            {
                _sessions.TryRemove(sessionId, out _);
                _logger.LogInformation("Session {SessionId} expired after being idle", sessionId);
                return null;
            }

            return session;
        }

        private bool IsExpired(ConnectionSession session) =>
            DateTime.UtcNow - session.LastAccessedAt > TimeSpan.FromMinutes(_settings.SessionIdleMinutes);

        private void RemoveExpiredSessions()
        {
            foreach (var (id, session) in _sessions)
                if (IsExpired(session))
                    _sessions.TryRemove(id, out _);
        }

        // ---- connection string ----------------------------------------------------------------

        // Built with SqlConnectionStringBuilder so user-supplied values are escaped, never concatenated
        // (a password like "x;Integrated Security=True" stays a password).
        private string BuildConnectionString(ConnectionDetails details)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = details.ServerName.Trim(),
                TrustServerCertificate = _settings.TrustServerCertificate,
                ConnectTimeout = 15,
                ApplicationName = "WebSql"
            };

            if (details.IntegratedSecurity)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = details.Login ?? string.Empty;
                builder.Password = details.Password ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(details.SelectedDatabase))
                builder.InitialCatalog = details.SelectedDatabase;

            return builder.ConnectionString;
        }

        private class ConnectionSession
        {
            public string SessionToken { get; set; } = string.Empty;
            public string ConnectionString { get; set; } = string.Empty;
            public ConnectionDetails ConnectionDetails { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessedAt { get; set; }
        }
    }
}
