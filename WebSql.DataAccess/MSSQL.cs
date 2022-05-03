using System.Data;
using System.Data.SqlClient;
using WebSql.Shared;

namespace WebSql.DataAccess
{
    public class MSSQL
    {
        private string _connectionString;

        public string ConnectionString { get => _connectionString; }

        public MSSQL(string connectionString)
		{
            _connectionString = connectionString;
        }

        public DataTable RunQuery(string query)
		{
            using SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();

            SqlCommand command = connection.CreateCommand();
            command.CommandText = query;

            DataTable dt = new DataTable();
            SqlDataAdapter da = new SqlDataAdapter(command);
            da.Fill(dt);
            connection.Close();
            da.Dispose();

            return dt;
        }
    }
}