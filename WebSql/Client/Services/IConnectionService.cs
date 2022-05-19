
using WebSql.Shared;

namespace WebSql.Client.Services
{
    public interface IConnectionService
    {
        ConnectionDetails ConnectionDetails { get; set; }

        string ConnectionString { get; }
        string SelectedDatabase { get; set; }
        IEnumerable<string> DatabaseNames { get; set; }

        ObjectExplorer ObjectExplorer { get; }

        Task<bool> GetExplorerAsync();

        Task<int> RunQuery(string query);

        Table Table { get; }
    }
}