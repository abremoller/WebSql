using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSql.Shared
{
    public class ConnectionDetails
    {
        public string ServerName { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public bool IntegratedSecurity { get; set; }
    }
}
