using System.Net.Http.Json;
using WebSql.Shared;
using WebSql.Shared.DTOs;

namespace WebSql.Client.Services
{
	public class ConnectionService : IConnectionService
	{
		private string? _sessionToken;
		private string _selectedDatabase = "master";
		private DatabaseEngine _engine = DatabaseEngine.SqlServer;
		private Table? _table;
		private ObjectExplorer? _objectExplorer = null;
		private readonly HttpClient _http;

		public ConnectionService(HttpClient http)
        {
			_http = http;
			ConnectionDetails = new ConnectionDetails();
		}

		public Table? Table => _table;
		public ObjectExplorer? ObjectExplorer => _objectExplorer;
		public string? SessionToken => _sessionToken;
		public bool Connected => !string.IsNullOrEmpty(_sessionToken);
		public DatabaseEngine Engine => _engine;
		public string SelectedDatabase 
		{ 
			get => _selectedDatabase; 
			set => _selectedDatabase = value; 
		}
		public IEnumerable<string> DatabaseNames { get; set; } = new List<string>();
        public ConnectionDetails ConnectionDetails { get; set; }
		public double LastQueryExecutionTimeMs { get; private set; }

		public async Task<bool> ConnectAsync()
		{
			try
			{
				Console.WriteLine($"[ConnectionService] Attempting connection to {ConnectionDetails.ServerName}");
				
				var request = new ConnectRequest
				{
					Engine = ConnectionDetails.Engine,
						ServerName = ConnectionDetails.ServerName,
					Login = ConnectionDetails.Login,
					Password = ConnectionDetails.Password,
					IntegratedSecurity = ConnectionDetails.IntegratedSecurity
				};

				var response = await _http.PostAsJsonAsync("api/Connection/connect", request);
				Console.WriteLine($"[ConnectionService] Connect response: {response.StatusCode}");
				
				if (response.IsSuccessStatusCode)
				{
					var result = await response.Content.ReadFromJsonAsync<ConnectResponse>();
					Console.WriteLine($"[ConnectionService] Connect result: Success={result?.Success}, Token={result?.SessionToken?.Substring(0, 10)}...");
					
					if (result?.Success == true)
					{
						_sessionToken = result.SessionToken;
						_engine = ConnectionDetails.Engine;
						// SQL Server sessions start in master; MySQL has no default until one is picked (see GetExplorerAsync).
						_selectedDatabase = _engine == DatabaseEngine.SqlServer ? "master" : string.Empty;
						return true;
					}
				}
				else
				{
					var errorContent = await response.Content.ReadAsStringAsync();
					Console.WriteLine($"[ConnectionService] Connect failed: {errorContent}");
				}

				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ConnectionService] Connect exception: {ex.Message}\n{ex.StackTrace}");
				return false;
			}
		}

        public async Task<bool> GetExplorerAsync()
		{
			if (string.IsNullOrEmpty(_sessionToken))
			{
				Console.WriteLine("[ConnectionService] GetExplorer: No session token");
				return false;
			}

			try
			{
				Console.WriteLine("[ConnectionService] Requesting object explorer");
				
				var request = new ObjectExplorerRequest { SessionToken = _sessionToken };
				var response = await _http.PostAsJsonAsync("api/Query/object-explorer", request);
				Console.WriteLine($"[ConnectionService] Explorer response: {response.StatusCode}");

				if (response.IsSuccessStatusCode)
				{
					var result = await response.Content.ReadFromJsonAsync<ObjectExplorerResponse>();
					Console.WriteLine($"[ConnectionService] Explorer result: Success={result?.Success}, Databases={result?.Explorer?.Server?.Databases?.Count}");
					
					if (result?.Success == true && result.Explorer != null)
					{
						_objectExplorer = result.Explorer;
						DatabaseNames = _objectExplorer.Server.Databases.Select(x => x.Name).ToList();
						Console.WriteLine($"[ConnectionService] Explorer loaded: {DatabaseNames.Count()} databases");

						if (_engine == DatabaseEngine.MySql && string.IsNullOrEmpty(_selectedDatabase))
						{
							var first = PickDefaultMySqlDatabase(DatabaseNames);
							if (first != null)
								await ChangeDatabaseAsync(first);
						}
						return true;
					}
				}
				else
				{
					var errorContent = await response.Content.ReadAsStringAsync();
					Console.WriteLine($"[ConnectionService] Explorer failed: {errorContent}");
				}

				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ConnectionService] Explorer exception: {ex.Message}\n{ex.StackTrace}");
				return false;
			}
		}

        public async Task<(int RowCount, string? ErrorMessage, bool RequiresConfirmation)> RunQuery(string query, bool confirmedDangerous = false)
        {
			if (string.IsNullOrEmpty(_sessionToken))
				return (0, "Not connected", false);

			try
			{
				var request = new QueryRequest 
				{ 
					SessionToken = _sessionToken, 
					Query = query,
					ConfirmedDangerous = confirmedDangerous
				};
				
				var response = await _http.PostAsJsonAsync("api/Query/execute", request);

				// Always try to read the response body for QueryResponse
				var result = await response.Content.ReadFromJsonAsync<QueryResponse>();
				
				if (response.IsSuccessStatusCode)
				{
					if (result?.Success == true && result.ResultTable != null)
					{
						_table = result.ResultTable;
						LastQueryExecutionTimeMs = result.ExecutionTimeMs;
						return (result.RowsAffected, null, false);
					}
					else
					{
						return (0, result?.ErrorMessage ?? "Unknown error", result?.RequiresConfirmation ?? false);
					}
				}
				else
				{
					// Check if it's a validation error requiring confirmation
					if (result != null)
					{
						return (0, result.ErrorMessage ?? $"HTTP {response.StatusCode}", result.RequiresConfirmation);
					}
					return (0, $"HTTP {response.StatusCode}", false);
				}
			}
			catch (Exception ex)
			{
				return (0, ex.Message, false);
			}
        }

		// Skip the built-in schemas so the first thing you see is your own data.
		private static readonly string[] MySqlSystemDatabases = { "information_schema", "mysql", "performance_schema", "sys" };

		private static string? PickDefaultMySqlDatabase(IEnumerable<string> names) =>
			names.FirstOrDefault(n => !MySqlSystemDatabases.Contains(n, StringComparer.OrdinalIgnoreCase)) ?? names.FirstOrDefault();

		public async Task<bool> ChangeDatabaseAsync(string databaseName)
		{
			if (string.IsNullOrEmpty(_sessionToken))
				return false;

			try
			{
				var request = new ChangeDatabaseRequest
				{
					SessionToken = _sessionToken,
					DatabaseName = databaseName
				};

				var response = await _http.PostAsJsonAsync("api/Connection/change-database", request);

				if (response.IsSuccessStatusCode)
				{
					_selectedDatabase = databaseName;
					return true;
				}

				return false;
			}
			catch
			{
				return false;
			}
		}

		public async Task DisconnectAsync()
		{
			if (string.IsNullOrEmpty(_sessionToken))
				return;

			try
			{
				var request = new DisconnectRequest { SessionToken = _sessionToken };
				await _http.PostAsJsonAsync("api/Connection/disconnect", request);
				_sessionToken = null;
				_objectExplorer = null;
				_table = null;
			}
			catch
			{
				// Ignore disconnect errors
			}
		}
    }
}
