using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebSql.DataAccess;
using WebSql.Shared;

namespace WebSql.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SQLController : ControllerBase
    {
        [HttpGet]
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
    }
}
