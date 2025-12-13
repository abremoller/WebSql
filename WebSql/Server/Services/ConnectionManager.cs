using System.Collections.Concurrent;
using WebSql.Shared;

namespace WebSql.Server.Services
{
    /// <summary>
    /// Manages database connections securely with JWT-based tokens
    /// </summary>
    public class ConnectionManager : IConnectionManager
    {
        private readonly ConcurrentDictionary<string, ConnectionSession> _sessions;
        private readonly ILogger<ConnectionManager> _logger;
        private readonly IJwtService _jwtService;

        public ConnectionManager(ILogger<ConnectionManager> logger, IJwtService jwtService)
        {
            _sessions = new ConcurrentDictionary<string, ConnectionSession>();
            _logger = logger;
            _jwtService = jwtService;
        }

        public Task<string> CreateConnectionAsync(ConnectionDetails connectionDetails)
        {
            try
            {
                // Validate connection details
                if (string.IsNullOrWhiteSpace(connectionDetails.ServerName))
                {
                    throw new ArgumentException("Server name is required");
                }

                // Generate a unique session ID
                string sessionId = Guid.NewGuid().ToString();

                // Build connection string
                string connectionString = BuildConnectionString(connectionDetails);

                // Create session
                var session = new ConnectionSession
                {
                    SessionToken = sessionId,
                    ConnectionString = connectionString,
                    ConnectionDetails = connectionDetails,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };

                _sessions.TryAdd(sessionId, session);

                // Generate JWT token
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
            var sessionId = _jwtService.GetSessionIdFromToken(jwtToken);
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Invalid JWT token");
                return null;
            }

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return session.ConnectionString;
            }

            _logger.LogWarning("Session not found: {SessionId}", sessionId);
            return null;
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
            {
                _logger.LogInformation("Disconnected session: {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("Attempted to disconnect non-existent session: {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }

        public bool ValidateSession(string jwtToken)
        {
            if (string.IsNullOrWhiteSpace(jwtToken))
                return false;

            // JWT validates expiration automatically
            var principal = _jwtService.ValidateToken(jwtToken);
            if (principal == null)
                return false;

            var sessionId = principal.FindFirst("sessionId")?.Value;
            if (string.IsNullOrEmpty(sessionId))
                return false;

            return _sessions.ContainsKey(sessionId);
        }

        public void UpdateDatabase(string jwtToken, string database)
        {
            var sessionId = _jwtService.GetSessionIdFromToken(jwtToken);
            if (string.IsNullOrEmpty(sessionId))
                return;

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                var details = session.ConnectionDetails;
                details.SelectedDatabase = database;
                session.ConnectionString = BuildConnectionString(details);
                session.LastAccessedAt = DateTime.UtcNow;

                _logger.LogInformation("Updated database for session {SessionId} to {Database}", 
                    sessionId, database);
            }
        }

        private string BuildConnectionString(ConnectionDetails details)
        {
            string connectionString = details.IntegratedSecurity
                ? $"Data Source={details.ServerName};Integrated Security=True;TrustServerCertificate=True;"
                : $"Data Source={details.ServerName};User Id={details.Login};Password={details.Password};TrustServerCertificate=True;";

            if (!string.IsNullOrWhiteSpace(details.SelectedDatabase))
            {
                connectionString += $"Initial Catalog={details.SelectedDatabase};";
            }

            return connectionString;
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
