using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSql.Shared
{
    public class Database
    {
        public Database()
        {
            Tables = new List<Table>();
            Views = new List<Script>();
            StoredProcedures = new List<Script>();
        }

        public string Name { get; set; }

        public List<Table> Tables { get; set; }
        public List<Script> Views { get; set; }
        public List<Script> StoredProcedures { get; set; }
    }
}
