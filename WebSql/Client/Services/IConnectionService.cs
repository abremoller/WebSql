using WebSql.Shared;

namespace WebSql.Client.Services
{
    public interface IConnectionService
    {
        ConnectionDetails ConnectionDetails { get; set; }
        string? SessionToken { get; }
        bool Connected { get; }
        /// <summary>The engine of the connected session (SQL Server until a connection says otherwise).</summary>
        DatabaseEngine Engine { get; }
        string SelectedDatabase { get; set; }
        IEnumerable<string> DatabaseNames { get; set; }
        ObjectExplorer? ObjectExplorer { get; }
        Table? Table { get; }
        double LastQueryExecutionTimeMs { get; }

        Task<bool> ConnectAsync();
        Task<bool> GetExplorerAsync();
        Task<(int RowCount, string? ErrorMessage, bool RequiresConfirmation)> RunQuery(string query, bool confirmedDangerous = false);
        Task<bool> ChangeDatabaseAsync(string databaseName);
        Task DisconnectAsync();
    }
}