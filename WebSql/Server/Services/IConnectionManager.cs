using WebSql.Shared;

namespace WebSql.Server.Services
{
    /// <summary>
    /// Manages database connections securely on the server side
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Creates a new connection and returns a session token
        /// </summary>
        Task<string> CreateConnectionAsync(ConnectionDetails connectionDetails);

        /// <summary>
        /// Gets the connection string for a given session token
        /// </summary>
        string? GetConnectionString(string sessionToken);

        /// <summary>
        /// Removes a connection session
        /// </summary>
        Task DisconnectAsync(string sessionToken);

        /// <summary>
        /// Validates that a session token exists and is valid
        /// </summary>
        bool ValidateSession(string sessionToken);

        /// <summary>
        /// Updates the active database for a session
        /// </summary>
        void UpdateDatabase(string sessionToken, string database);
    }
}
