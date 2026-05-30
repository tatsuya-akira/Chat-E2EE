using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;

namespace Hermes.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection") ?? "";

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("check-identifier")]
        public async Task<IActionResult> CheckIdentifier([FromQuery] string identifier)
        {
            using var connection = new MySqlConnection(ConnectionString);
            string query = @"
                SELECT i.FullName FROM USERS u
                JOIN USERINFO i ON u.Id = i.UserId
                WHERE u.Email = @Iden OR i.FullName = @Iden LIMIT 1";

            var result = await connection.QueryFirstOrDefaultAsync<string>(query, new { Iden = identifier });
            if (result != null) return BadRequest("Identifier already exists.");
            return Ok();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Hermes.Shared.DTOs.RegisterRequest request)
        {
            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                string insertUserQuery = @"
                    INSERT INTO USERS (Id, Email, PublicKey, WrappedPrivateKey, Salt) 
                    VALUES (@Id, @Email, @PublicKey, @WrappedPrivateKey, @Salt)";
                await connection.ExecuteAsync(insertUserQuery, request, transaction);

                string insertInfoQuery = "INSERT INTO USERINFO (UserId, FullName) VALUES (@Id, @FullName)";
                await connection.ExecuteAsync(insertInfoQuery, request, transaction);

                await transaction.CommitAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Avoid sharing internal exceptions to clients in production
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("keys/{userId}")]
        public async Task<ActionResult<Hermes.Shared.DTOs.UserKeysResponse>> GetKeys(string userId)
        {
            using var connection = new MySqlConnection(ConnectionString);
            string query = "SELECT PublicKey, WrappedPrivateKey, Salt FROM USERS WHERE Id = @Id LIMIT 1";
            var keys = await connection.QueryFirstOrDefaultAsync<Hermes.Shared.DTOs.UserKeysResponse>(query, new { Id = userId });
            
            if (keys == null) return NotFound();
            return Ok(keys);
        }
        [HttpPut("update-keys")]
        public async Task<IActionResult> UpdateKeys([FromBody] Hermes.Shared.DTOs.UpdateKeyRequest request)
        {
            using var connection = new MySqlConnection(ConnectionString);
            string query = @"
                UPDATE USERS 
                SET PublicKey = @PublicKey, WrappedPrivateKey = @WrappedPrivateKey, Salt = @Salt 
                WHERE Id = @UserId";

            int rowsAffected = await connection.ExecuteAsync(query, request);

            if (rowsAffected > 0) return Ok();
            return BadRequest("Không tìm thấy tài khoản để cập nhật khóa.");
        }
    }
}
