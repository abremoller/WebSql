using WebSql.Shared;

namespace WebSql.Client.Services
{
    public interface IConnectionService
    {
        ConnectionDetails ConnectionDetails { get; set; }
        string? SessionToken { get; }
        string SelectedDatabase { get; set; }
        IEnumerable<string> DatabaseNames { get; set; }
        ObjectExplorer? ObjectExplorer { get; }
        Table? Table { get; }
        double LastQueryExecutionTimeMs { get; }

        Task<bool> ConnectAsync();
        Task<bool> GetExplorerAsync();
        Task<(int RowCount, string? ErrorMessage)> RunQuery(string query);
        Task<bool> ChangeDatabaseAsync(string databaseName);
        Task DisconnectAsync();
    }
}