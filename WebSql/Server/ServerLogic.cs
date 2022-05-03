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

		private List<string> _databases;
		private string _connectionString;

		private void PopulateDatabases()
		{
			string conn = _connectionString + $"Initial Catalog={__MasterDBName}";

			_databases = new List<string>();

			MSSQL sql = new MSSQL(conn);
			var dt = sql.RunQuery(__GetDatabases);

			foreach (DataRow row in dt.Rows)
				_databases.Add(row["name"].ToString());
		}

		public ObjectExplorer ReadObjectExplorer()
		{
			ObjectExplorer objectExplorer = new ObjectExplorer();
			objectExplorer.Server = new SQLServer();
			objectExplorer.Server.Databases = new List<Database>();
			foreach (string dbName in _databases)
			{
				var db = new Database() { Name = dbName };
				PopulateDatabaseStructures(db);
				objectExplorer.Server.Databases.Add(db);

			}

			return objectExplorer;
		}

		private void PopulateDatabaseStructures(Database db)
		{
			string conn = _connectionString + $"Initial Catalog={db.Name}";
			MSSQL sql = new MSSQL(conn);
			db.Tables = new List<Table>();
			var dt = sql.RunQuery(__GetTables);

			DataView view = new DataView(dt);
			DataTable distinctValues = view.ToTable(true, "TABLE_NAME");

			foreach (DataRow dr in distinctValues.Rows)
			{
				string tableName = dr["TABLE_NAME"].ToString();
				db.Tables.Add(new Table() { Name = tableName, Columns = GetColumns(dt, tableName) });
			}
		}

        private List<Column> GetColumns(DataTable dt, string tableName)
        {
			List<Column> columns = new List<Column>();

			try
			{
				var columnDs = dt.Select($"TABLE_NAME = '{tableName}'");
				foreach (DataRow dr in columnDs)
				{
					columns.Add(new Column() { Name = dr["COLUMN_NAME"].ToString() });
				}
			} catch (Exception ex)
            {
				//Do Nothing for now
            }

			return columns;
        }

        public static ObjectExplorer GetObjectExplorer(string connectionString)
        {
			ServerLogic logic = new ServerLogic();
			logic._connectionString = connectionString;
			logic.PopulateDatabases();
			return logic.ReadObjectExplorer();
        }
    }
}
