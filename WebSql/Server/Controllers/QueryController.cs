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
        private readonly QueryValidator _queryValidator;

        public QueryController(IConnectionManager connectionManager, ILogger<QueryController> logger, IConfiguration configuration)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            
            // Read security mode from configuration
            var modeString = configuration["Security:DangerousOperationsMode"] ?? "Prompt";
            var mode = Enum.Parse<DangerousOperationsMode>(modeString, ignoreCase: true);
            _queryValidator = new QueryValidator(mode);
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

                // Validate query for dangerous operations
                var validationResult = _queryValidator.Validate(request.Query, request.ConfirmedDangerous);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Dangerous query blocked: {DangerousOp} - Query: {Query}", 
                        validationResult.DangerousOperation, 
                        request.Query.Substring(0, Math.Min(100, request.Query.Length)));
                    
                    return BadRequest(new QueryResponse
                    {
                        Success = false,
                        ErrorMessage = validationResult.ErrorMessage,
                        RequiresConfirmation = validationResult.RequiresConfirmation
                    });
                }

                // Log warning if query has warnings
                if (!string.IsNullOrEmpty(validationResult.WarningMessage))
                {
                    _logger.LogWarning("Query warning: {Warning}", validationResult.WarningMessage);
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
