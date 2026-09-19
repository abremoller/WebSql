using Microsoft.AspNetCore.Mvc;
using WebSql.Server.Services;
using WebSql.Shared;
using WebSql.Shared.DTOs;

namespace WebSql.Server.Controllers
{
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("fixed")]
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionController : ControllerBase
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<ConnectionController> _logger;

        public ConnectionController(IConnectionManager connectionManager, ILogger<ConnectionController> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        [HttpPost("connect")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("connect")]
        public async Task<ActionResult<ConnectResponse>> Connect([FromBody] ConnectRequest request)
        {
            try
            {
                var connectionDetails = new ConnectionDetails
                {
                    ServerName = request.ServerName,
                    Login = request.Login,
                    Password = request.Password,
                    IntegratedSecurity = request.IntegratedSecurity
                };

                var sessionToken = await _connectionManager.CreateConnectionAsync(connectionDetails);

                return Ok(new ConnectResponse
                {
                    SessionToken = sessionToken,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection failed");
                return BadRequest(new ConnectResponse
                {
                    SessionToken = string.Empty,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost("disconnect")]
        public async Task<ActionResult<ApiResponse>> Disconnect([FromBody] DisconnectRequest request)
        {
            try
            {
                await _connectionManager.DisconnectAsync(request.SessionToken);
                return Ok(new ApiResponse { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disconnect failed");
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost("change-database")]
        public ActionResult<ApiResponse> ChangeDatabase([FromBody] ChangeDatabaseRequest request)
        {
            try
            {
                if (!_connectionManager.ValidateSession(request.SessionToken))
                {
                    return Unauthorized(new ApiResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid or expired session"
                    });
                }

                _connectionManager.UpdateDatabase(request.SessionToken, request.DatabaseName);
                return Ok(new ApiResponse { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Change database failed");
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
