using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using Hermes.Shared.DTOs;

namespace Hermes.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection") ?? "";

        public ConversationController(IConfiguration configuration) { _configuration = configuration; }

        // GET: api/Conversation/user/{identifier}
        [HttpGet("user/{identifier}")]
        public async Task<IActionResult> GetUserByIdentifier(string identifier)
        {
            using var connection = new MySqlConnection(ConnectionString);
            string query = @"
                SELECT u.Id as UserId, i.FullName 
                FROM USERS u 
                JOIN USERINFO i ON u.Id = i.UserId 
                WHERE u.Email = @Iden OR i.FullName = @Iden LIMIT 1";

            var user = await connection.QueryFirstOrDefaultAsync<UserInfoResponse>(query, new { Iden = identifier });

            if (user == null) return NotFound();
            return Ok(user);
        }

        // POST: api/Conversation
        [HttpPost]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            // Đưa TẤT CẢ vào trong try...catch để bắt mọi loại lỗi
            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync(); // Nếu rớt mạng VPS, nó sẽ nhảy thẳng xuống catch
                using var transaction = await connection.BeginTransactionAsync();

                string insertConv = "INSERT INTO CONVERSATIONS (IsGroup, GroupName) VALUES (@IsGroup, @GroupName); SELECT LAST_INSERT_ID();";

                // Hứng ID chống lỗi ép kiểu
                var resultObj = await connection.ExecuteScalarAsync<object>(insertConv, request, transaction);
                int convId = Convert.ToInt32(resultObj);

                var uniqueParticipantIds = request.ParticipantIds.Distinct().ToList();

                string insertPart = "INSERT INTO PARTICIPANTS (ConversationId, UserId) VALUES (@ConvId, @UserId)";
                foreach (var uid in uniqueParticipantIds)
                {
                    if (!string.IsNullOrEmpty(uid))
                    {
                        await connection.ExecuteAsync(insertPart, new { ConvId = convId, UserId = uid }, transaction);
                    }
                }

                await transaction.CommitAsync();
                return Ok(convId);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n=== LỖI MYSQL KHI TẠO CHAT ===");
                Console.WriteLine(ex.Message);
                Console.WriteLine($"==============================\n");
                Console.ResetColor();

                return StatusCode(500, ex.Message);
            }
        }
        // GET: api/Conversation/my-chats/{userId}
        [HttpGet("my-chats/{userId}")]
        public async Task<IActionResult> GetMyChats(string userId)
        {
            using var connection = new MySqlConnection(ConnectionString);

            // Câu truy vấn tìm các nhóm/người đã chat, và lấy tên người bên kia (nếu là chat cá nhân)
            string query = @"
        SELECT 
            c.Id as ChatId, 
            c.IsGroup, 
            c.GroupName,
            (SELECT i.FullName 
             FROM PARTICIPANTS p2 
             JOIN USERINFO i ON p2.UserId = i.UserId 
             WHERE p2.ConversationId = c.Id AND p2.UserId != @UserId LIMIT 1) as OtherUserName
        FROM CONVERSATIONS c
        JOIN PARTICIPANTS p ON c.Id = p.ConversationId
        WHERE p.UserId = @UserId
        ORDER BY c.CreatedAt DESC";

            var chats = await connection.QueryAsync<ChatListResponse>(query, new { UserId = userId });
            return Ok(chats);
        }
    }
}