using System.Data;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using WebSql.DataAccess;
using WebSql.Shared;

namespace WebSql.Server
{
    public class ServerLogic
    {
		const string __GetDatabases = "select * from sys.databases order by name asc";
		const string __GetMySqlDatabases = "select SCHEMA_NAME as name from INFORMATION_SCHEMA.SCHEMATA order by SCHEMA_NAME asc";
		const string __GetTables = "select * from INFORMATION_SCHEMA.COLUMNS";
		const string __MasterDBName = "master";

		private List<string> _databases = new();
		private string _connectionString = string.Empty;
		private DatabaseEngine _engine = DatabaseEngine.SqlServer;

		private async Task PopulateDatabasesAsync()
		{
			string conn;
			string query;

			if (_engine == DatabaseEngine.MySql)
			{
				// MySQL can list every schema without selecting one first.
				conn = _connectionString;
				query = __GetMySqlDatabases;
			}
			else
			{
				// Use the builder rather than string-appending, so values are escaped properly.
				var connBuilder = new SqlConnectionStringBuilder(_connectionString);
				if (string.IsNullOrWhiteSpace(connBuilder.InitialCatalog))
				{
					connBuilder.InitialCatalog = __MasterDBName;
				}
				conn = connBuilder.ConnectionString;
				query = __GetDatabases;
			}

			_databases = new List<string>();

			var sql = new DatabaseRunner(_engine, conn);
			var (dt, _, _) = await sql.RunQueryAsync(query);

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
		string conn;
		string query = __GetTables;

		// Switch the catalog via the builder: a database name containing ';' or '=' can't inject keywords.
		if (_engine == DatabaseEngine.MySql)
		{
			conn = new MySqlConnectionStringBuilder(_connectionString) { Database = db.Name }.ConnectionString;
			// In MySQL a "schema" is a database, so TABLE_SCHEMA is the database name. Filter to this one.
			query = "select TABLE_SCHEMA as TABLE_SCHEMA, TABLE_NAME as TABLE_NAME, COLUMN_NAME as COLUMN_NAME, DATA_TYPE as DATA_TYPE " +
				"from INFORMATION_SCHEMA.COLUMNS " +
				$"where TABLE_SCHEMA = '{_engine.EscapeStringLiteral(db.Name)}' " +
				"order by TABLE_NAME, ORDINAL_POSITION";
		}
		else
		{
			conn = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = db.Name }.ConnectionString;
		}

		var sql = new DatabaseRunner(_engine, conn);
		db.Tables = new List<Table>();
		var (dt, _, _) = await sql.RunQueryAsync(query);

		DataView view = new DataView(dt);
		DataTable distinctValues = view.ToTable(true, "TABLE_SCHEMA", "TABLE_NAME");

		foreach (DataRow dr in distinctValues.Rows)
		{
			string? tableName = dr["TABLE_NAME"]?.ToString();
			string? schemaName = dr["TABLE_SCHEMA"]?.ToString();
			if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(schemaName))
			{
				db.Tables.Add(new Table() { Name = tableName, Schema = schemaName, Columns = GetColumns(dt, schemaName, tableName) });
			}
		}
	}

	private List<Column> GetColumns(DataTable dt, string schemaName, string tableName)
        {
			List<Column> columns = new List<Column>();

			try
			{
				// Escape single quotes to prevent SQL injection in DataTable.Select
				var escapedSchemaName = schemaName.Replace("'", "''");
				var escapedTableName = tableName.Replace("'", "''");
				var columnDs = dt.Select($"TABLE_SCHEMA = '{escapedSchemaName}' AND TABLE_NAME = '{escapedTableName}'");
				foreach (DataRow dr in columnDs)
				{
					var columnName = dr["COLUMN_NAME"]?.ToString();
					var dataType = dr["DATA_TYPE"]?.ToString();
					if (!string.IsNullOrEmpty(columnName))
					{
						columns.Add(new Column() { Name = columnName, Type = dataType ?? "unknown" });
					}
				}
			}
			catch (Exception ex)
            {
				Console.WriteLine($"Error getting columns for table {schemaName}.{tableName}: {ex.Message}");
            }

			return columns;
        }

        public static async Task<ObjectExplorer> GetObjectExplorerAsync(DatabaseEngine engine, string connectionString)
        {
			ServerLogic logic = new ServerLogic();
			logic._engine = engine;
			logic._connectionString = connectionString;
			await logic.PopulateDatabasesAsync();
			return await logic.ReadObjectExplorerAsync();
        }

		public static async Task<(Table Table, int RowsAffected, double ExecutionTimeMs)> RunQueryAsync(DatabaseEngine engine, string connectionString, string query, int maxRows = 0, int timeoutSeconds = 300)
        {
			var sql = new DatabaseRunner(engine, connectionString, maxRows, timeoutSeconds);
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
