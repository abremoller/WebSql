using System.Collections.Concurrent;
using WebSql.Shared;

namespace WebSql.Server.Services
{
    /// <summary>
    /// Manages database connections securely with session-based tokens
    /// </summary>
    public class ConnectionManager : IConnectionManager
    {
        private readonly ConcurrentDictionary<string, ConnectionSession> _sessions;
        private readonly ILogger<ConnectionManager> _logger;

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _sessions = new ConcurrentDictionary<string, ConnectionSession>();
            _logger = logger;
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

                // Generate a unique session token
                string sessionToken = Guid.NewGuid().ToString();

                // Build connection string
                string connectionString = BuildConnectionString(connectionDetails);

                // Create session
                var session = new ConnectionSession
                {
                    SessionToken = sessionToken,
                    ConnectionString = connectionString,
                    ConnectionDetails = connectionDetails,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };

                _sessions.TryAdd(sessionToken, session);

                _logger.LogInformation("Created new connection session: {SessionToken} for server: {Server}", 
                    sessionToken, connectionDetails.ServerName);

                return Task.FromResult(sessionToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating connection session");
                throw;
            }
        }

        public string? GetConnectionString(string sessionToken)
        {
            if (_sessions.TryGetValue(sessionToken, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return session.ConnectionString;
            }

            _logger.LogWarning("Session token not found: {SessionToken}", sessionToken);
            return null;
        }

        public Task DisconnectAsync(string sessionToken)
        {
            if (_sessions.TryRemove(sessionToken, out _))
            {
                _logger.LogInformation("Disconnected session: {SessionToken}", sessionToken);
            }
            else
            {
                _logger.LogWarning("Attempted to disconnect non-existent session: {SessionToken}", sessionToken);
            }

            return Task.CompletedTask;
        }

        public bool ValidateSession(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                return false;

            if (_sessions.TryGetValue(sessionToken, out var session))
            {
                // Check if session has expired (24 hours)
                if (DateTime.UtcNow - session.CreatedAt > TimeSpan.FromHours(24))
                {
                    _sessions.TryRemove(sessionToken, out _);
                    _logger.LogInformation("Session expired and removed: {SessionToken}", sessionToken);
                    return false;
                }

                return true;
            }

            return false;
        }

        public void UpdateDatabase(string sessionToken, string database)
        {
            if (_sessions.TryGetValue(sessionToken, out var session))
            {
                var details = session.ConnectionDetails;
                details.SelectedDatabase = database;
                session.ConnectionString = BuildConnectionString(details);
                session.LastAccessedAt = DateTime.UtcNow;

                _logger.LogInformation("Updated database for session {SessionToken} to {Database}", 
                    sessionToken, database);
            }
        }

        private string BuildConnectionString(ConnectionDetails details)
        {
            string connectionString = details.IntegratedSecurity
                ? $"Data Source={details.ServerName};Integrated Security=True;"
                : $"Data Source={details.ServerName};User Id={details.Login};Password={details.Password};";

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
