using Microsoft.AspNetCore.Mvc;
using WebSql.Server.Services;
using WebSql.Shared.DTOs;

namespace WebSql.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<QueryController> _logger;

        public QueryController(IConnectionManager connectionManager, ILogger<QueryController> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        [HttpPost("execute")]
        public async Task<ActionResult<QueryResponse>> ExecuteQuery([FromBody] QueryRequest request)
        {
            try
            {
                if (!_connectionManager.ValidateSession(request.SessionToken))
                {
                    return Unauthorized(new QueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid or expired session"
                    });
                }

                var connectionString = _connectionManager.GetConnectionString(request.SessionToken);
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest(new QueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Connection not found"
                    });
                }

                var table = await ServerLogic.RunQueryAsync(connectionString, request.Query);

                return Ok(new QueryResponse
                {
                    ResultTable = table.Table,
                    Success = true,
                    RowsAffected = table.RowsAffected,
                    ExecutionTimeMs = table.ExecutionTimeMs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Query execution failed");
                return BadRequest(new QueryResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost("object-explorer")]
        public async Task<ActionResult<ObjectExplorerResponse>> GetObjectExplorer([FromBody] ObjectExplorerRequest request)
        {
            try
            {
                if (!_connectionManager.ValidateSession(request.SessionToken))
                {
                    return Unauthorized(new ObjectExplorerResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid or expired session"
                    });
                }

                var connectionString = _connectionManager.GetConnectionString(request.SessionToken);
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest(new ObjectExplorerResponse
                    {
                        Success = false,
                        ErrorMessage = "Connection not found"
                    });
                }

                var explorer = await ServerLogic.GetObjectExplorerAsync(connectionString);

                return Ok(new ObjectExplorerResponse
                {
                    Explorer = explorer,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Object explorer retrieval failed");
                return BadRequest(new ObjectExplorerResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
