using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using WebSql.Shared;

namespace WebSql.DataAccess
{
    /// <summary>
    /// Runs queries against SQL Server or MySQL/MariaDB. Each call opens (and disposes) its own connection.
    /// </summary>
    public class DatabaseRunner
    {
        private readonly DatabaseEngine _engine;
        private readonly string _connectionString;

        public string ConnectionString => _connectionString;

        public DatabaseRunner(DatabaseEngine engine, string connectionString)
        {
            _engine = engine;
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        private DbConnection CreateConnection() => _engine switch
        {
            DatabaseEngine.MySql => new MySqlConnection(_connectionString),
            _ => new SqlConnection(_connectionString)
        };

        /// <summary>
        /// Executes a query and returns the results with execution metrics
        /// </summary>
        public async Task<(DataTable Result, double ExecutionTimeMs, int RowsAffected)> RunQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be empty", nameof(query));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await using var connection = CreateConnection();
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = query;
                command.CommandTimeout = 300; // 5 minutes

                var dt = new DataTable();
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    dt.Load(reader);
                }

                stopwatch.Stop();

                return (dt, stopwatch.Elapsed.TotalMilliseconds, dt.Rows.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                throw new InvalidOperationException($"Query execution failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Tests the connection to ensure it's valid
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var connection = CreateConnection();
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
