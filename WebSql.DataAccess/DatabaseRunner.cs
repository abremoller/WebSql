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

        private readonly int _maxRows;
        private readonly int _timeoutSeconds;

        /// <param name="maxRows">Queries returning more rows than this fail instead of exhausting memory. 0 = no limit.</param>
        public DatabaseRunner(DatabaseEngine engine, string connectionString, int maxRows = 0, int timeoutSeconds = 300)
        {
            _maxRows = maxRows;
            _timeoutSeconds = timeoutSeconds;
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
                command.CommandTimeout = _timeoutSeconds;

                var dt = new DataTable();
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    // First result set only (as DataTable.Load did), read row by row so the cap applies while streaming.
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (string.IsNullOrEmpty(name) || dt.Columns.Contains(name)) name = $"{(string.IsNullOrEmpty(name) ? "Column" : name)}{i + 1}";
                        dt.Columns.Add(name, reader.GetFieldType(i) ?? typeof(object));
                    }

                    var values = new object[reader.FieldCount];
                    while (await reader.ReadAsync())
                    {
                        if (_maxRows > 0 && dt.Rows.Count >= _maxRows)
                            throw new InvalidOperationException(
                                $"The result has more than {_maxRows:N0} rows. Narrow it with WHERE, TOP or LIMIT.");

                        reader.GetValues(values);
                        dt.Rows.Add(values);
                    }
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
