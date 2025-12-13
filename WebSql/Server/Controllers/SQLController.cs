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
        public async Task<IActionResult> GetServerExplorer(string connectionString)
        {
            try
            {
                var objectExplorer = await ServerLogic.GetObjectExplorerAsync(connectionString);
                return Ok(objectExplorer);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("SQL/RunQuery/{connectionString}&{query}")]
        public async Task<IActionResult> RunQuery(string connectionString, string query)
        {
            try
            {
                var tableResults = await ServerLogic.RunQueryAsync(connectionString, query);
                return Ok(tableResults);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
