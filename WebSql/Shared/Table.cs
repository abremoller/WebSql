namespace WebSql.Shared
{
    public class Table
    {
        public string Name { get; set; }
        public List<Column> Columns {  get; set;}

        public List<List<string>>? Rows { get; set; }
    }
}