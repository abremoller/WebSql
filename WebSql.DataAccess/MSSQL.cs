using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace WebSql.DataAccess
{
    public class MSSQL
    {
        private readonly string _connectionString;

        public string ConnectionString => _connectionString;

        public MSSQL(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

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
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = query;
                command.CommandTimeout = 300; // 5 minutes

                var dt = new DataTable();
                using var da = new SqlDataAdapter(command);
                da.Fill(dt);

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
        /// Legacy synchronous method - use RunQueryAsync instead
        /// </summary>
        [Obsolete("Use RunQueryAsync instead")]
        public DataTable RunQuery(string query)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var dt = new DataTable();
            using var da = new SqlDataAdapter(command);
            da.Fill(dt);

            return dt;
        }

        /// <summary>
        /// Tests the connection to ensure it's valid
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
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