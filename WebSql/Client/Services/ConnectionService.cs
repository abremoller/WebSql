using System.Net.Http.Json;
using WebSql.DataAccess;
using WebSql.Shared;

namespace WebSql.Client.Services
{
	public class ConnectionService : IConnectionService
	{
		private string _connectionString;
		private List<string> _databases;
		private string _selectedDatabase;
		private Table _table;

		private ObjectExplorer _objectExplorer = null;
		private readonly HttpClient _http;


		public ConnectionService(HttpClient http)
        {
			_http = http;
			ConnectionDetails = new ConnectionDetails();
		}

		public ObjectExplorer ObjectExplorer { get => _objectExplorer; private set => _objectExplorer = value; }

		public string ConnectionString { get => GetConnectionString(); }

		public string SelectedDatabase { get => _selectedDatabase; set => _selectedDatabase = value; }
		public IEnumerable<string> DatabaseNames { get; set; }
        public ConnectionDetails ConnectionDetails { get; set; }

        public async Task<bool> GetExplorerAsync()
		{
			ObjectExplorer = await _http.GetFromJsonAsync<ObjectExplorer>($"SQL/GetServerExplorer/{ConnectionString}");

			return true;
		}

		private string GetConnectionString()
		{
			return ConnectionDetails.IntegratedSecurity
				? $"Data Source={ConnectionDetails.ServerName};Integrated Security=True;"
				: $"Data Source={ConnectionDetails.ServerName};User Id={ConnectionDetails.Login};Password={ConnectionDetails.Password};";
		}

        public async Task<int> RunQuery(string query)
        {
            _table = await _http.GetFromJsonAsync<Table>($"SQL/RunQuery/{ConnectionString}&{query}");

			return _table.Rows.Count;
        }
    }
}
