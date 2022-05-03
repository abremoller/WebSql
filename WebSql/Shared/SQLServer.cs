using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSql.Shared
{
    public class SQLServer
    {
        public string ServerName { get; set; }
        public string ServerVersion { get; set; }
        public List<Database> Databases { get; set; }
    }
}
