using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebSql.DataAccess;
using WebSql.Shared;

namespace WebSql.Server.Controllers
{
    
    [ApiController]
    public class SQLController : ControllerBase
    {
        [HttpGet]
        [Route("SQL/GetServerExplorer/{connectionString}")]
        public IActionResult GetServerExplorer(string connectionString)
        {
            try
            {
                var objectExplorer = ServerLogic.GetObjectExplorer(connectionString);
                return Ok(objectExplorer);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("SQL/RunQuery/{connectionString}&{query}")]
        public IActionResult RunQuery(string connectionString, string query)
        {
            try
            {
                var tableResults = ServerLogic.RunQuery(connectionString, query);
                return Ok(tableResults);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
