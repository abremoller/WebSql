using System.Data;
using WebSql.DataAccess;
using WebSql.Shared;

namespace WebSql.Server
{
    public class ServerLogic
    {
		const string __GetDatabases = "select * from sys.databases order by name asc";
		const string __GetTables = "select * from INFORMATION_SCHEMA.COLUMNS";
		const string __MasterDBName = "master";

		private List<string> _databases = new();
		private string _connectionString = string.Empty;

		private async Task PopulateDatabasesAsync()
		{
			string conn = _connectionString;
			if (!conn.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase))
			{
				conn += $"Initial Catalog={__MasterDBName};";
			}

			_databases = new List<string>();

			MSSQL sql = new MSSQL(conn);
			var (dt, _, _) = await sql.RunQueryAsync(__GetDatabases);

			foreach (DataRow row in dt.Rows)
			{
				var dbName = row["name"]?.ToString();
				if (!string.IsNullOrEmpty(dbName))
				{
					_databases.Add(dbName);
				}
			}
		}

		public async Task<ObjectExplorer> ReadObjectExplorerAsync()
		{
			ObjectExplorer objectExplorer = new ObjectExplorer();
			objectExplorer.Server = new SQLServer();
			objectExplorer.Server.Databases = new List<Database>();
			
			foreach (string dbName in _databases)
			{
				var db = new Database() { Name = dbName };

				try
                {
					await PopulateDatabaseStructuresAsync(db);
				}
                catch (Exception ex)
                {
					db.Name += " (unavailable)";
					// Log the error but continue processing other databases
					Console.WriteLine($"Error loading database {dbName}: {ex.Message}");
                }

					objectExplorer.Server.Databases.Add(db);
		}

		return objectExplorer;
	}

	private async Task PopulateDatabaseStructuresAsync(Database db)
	{
		string conn = _connectionString;
		// Remove any existing Initial Catalog and add the new one
		if (conn.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase))
		{
			var parts = conn.Split(';');
			conn = string.Join(";", parts.Where(p => !p.Trim().StartsWith("Initial Catalog", StringComparison.OrdinalIgnoreCase))) + ";";
		}
		conn += $"Initial Catalog={db.Name};";
		
		MSSQL sql = new MSSQL(conn);
		db.Tables = new List<Table>();
		var (dt, _, _) = await sql.RunQueryAsync(__GetTables);

		DataView view = new DataView(dt);
		DataTable distinctValues = view.ToTable(true, "TABLE_NAME");

		foreach (DataRow dr in distinctValues.Rows)
		{
			string? tableName = dr["TABLE_NAME"]?.ToString();
			if (!string.IsNullOrEmpty(tableName))
			{
				db.Tables.Add(new Table() { Name = tableName, Columns = GetColumns(dt, tableName) });
			}
		}
	}

	private List<Column> GetColumns(DataTable dt, string tableName)
        {
			List<Column> columns = new List<Column>();

			try
			{
				// Escape single quotes in table name to prevent SQL injection in DataTable.Select
				var escapedTableName = tableName.Replace("'", "''");
				var columnDs = dt.Select($"TABLE_NAME = '{escapedTableName}'");
				foreach (DataRow dr in columnDs)
				{
					var columnName = dr["COLUMN_NAME"]?.ToString();
					if (!string.IsNullOrEmpty(columnName))
					{
						columns.Add(new Column() { Name = columnName });
					}
				}
			}
			catch (Exception ex)
            {
				Console.WriteLine($"Error getting columns for table {tableName}: {ex.Message}");
            }

			return columns;
        }

        public static async Task<ObjectExplorer> GetObjectExplorerAsync(string connectionString)
        {
			ServerLogic logic = new ServerLogic();
			logic._connectionString = connectionString;
			await logic.PopulateDatabasesAsync();
			return await logic.ReadObjectExplorerAsync();
        }

		public static async Task<(Table Table, int RowsAffected, double ExecutionTimeMs)> RunQueryAsync(string connectionString, string query)
        {
			MSSQL sql = new MSSQL(connectionString);
			var (dt, executionTime, rowsAffected) = await sql.RunQueryAsync(query);

			Table table = new Table();
			table.Columns = new List<Column>();

			foreach (DataColumn dc in dt.Columns)
				table.Columns.Add(new Column() { Name = dc.ColumnName, Type = dc.DataType.ToString() });

			List<List<string>> rows = new List<List<string>>();
			foreach (DataRow dr in dt.Rows)
			{
				List<string> columns = new List<string>();

				foreach (var c in table.Columns)
				{
					var value = dr[c.Name];
					columns.Add(value?.ToString() ?? string.Empty);
				}

				rows.Add(columns);
			}

			table.Rows = rows;

			return (table, rowsAffected, executionTime);
		}
    }
}
