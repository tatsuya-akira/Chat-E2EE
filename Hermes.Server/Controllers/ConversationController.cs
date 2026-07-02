using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using Hermes.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Hermes.Server.Hubs;

namespace Hermes.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ChatHub> _hubContext;
        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection") ?? "";

        public ConversationController(IConfiguration configuration, IHubContext<ChatHub> hubContext) 
        { 
            _configuration = configuration; 
            _hubContext = hubContext;
        }

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

        // GET: api/Conversation/search-users?keyword=...
        [HttpGet("search-users")]
        public async Task<IActionResult> SearchUsers([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return Ok(new List<UserInfoResponse>());
            using var connection = new MySqlConnection(ConnectionString);
            string query = @"
                SELECT u.Id as UserId, i.FullName 
                FROM USERS u 
                JOIN USERINFO i ON u.Id = i.UserId 
                WHERE (u.Email LIKE @Kw OR i.FullName LIKE @Kw) AND u.Id != 'SYSTEM' LIMIT 15";

            var users = await connection.QueryAsync<UserInfoResponse>(query, new { Kw = $"%{keyword.Trim()}%" });
            return Ok(users);
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
                                (SELECT i.FullName FROM PARTICIPANTS p2 JOIN USERINFO i ON p2.UserId = i.UserId WHERE p2.ConversationId = c.Id AND p2.UserId != @UserId LIMIT 1) as OtherUserName,
                                (SELECT p2.UserId FROM PARTICIPANTS p2 WHERE p2.ConversationId = c.Id AND p2.UserId != @UserId LIMIT 1) as OtherUserId,
                                NOT EXISTS (
                                    SELECT 1 FROM MESSAGES m 
                                    JOIN MESSAGE_RECIPIENTS mr ON m.Id = mr.MessageId 
                                    WHERE m.ConversationId = c.Id AND mr.RecipientId = @UserId AND mr.IsRead = 0 AND m.SenderId != @UserId
                                ) as IsRead
                            FROM CONVERSATIONS c
                            JOIN PARTICIPANTS p ON c.Id = p.ConversationId
                            WHERE p.UserId = @UserId
                            ORDER BY c.CreatedAt DESC";

            var chats = await connection.QueryAsync<ChatListResponse>(query, new { UserId = userId });
            return Ok(chats);
        }

        [HttpGet("{conversationId}/public-keys")]
        public async Task<IActionResult> GetParticipantPublicKeys(int conversationId)
        {
            using var connection = new MySqlConnection(ConnectionString);

            // Câu truy vấn lấy Public Key của tất cả user đang nằm trong Conversation này
            string query = @"
        SELECT u.Id as UserId, u.PublicKey 
        FROM PARTICIPANTS p
        JOIN USERS u ON p.UserId = u.Id
        WHERE p.ConversationId = @ConvId";

            var keys = await connection.QueryAsync(query, new { ConvId = conversationId });

            // Trả về một Dictionary dạng { "UserId": "PublicKey_Base64" }
            var result = keys.ToDictionary(k => (string)k.UserId, k => (string)k.PublicKey);

            return Ok(result);
        }
        [HttpGet("username/{userId}")]
        public async Task<IActionResult> GetUsername(string userId)
        {
            using var connection = new MySqlConnection(ConnectionString);
            string query = "SELECT FullName FROM USERINFO WHERE UserId = @Uid LIMIT 1";
            var name = await connection.QueryFirstOrDefaultAsync<string>(query, new { Uid = userId });
            if (string.IsNullOrEmpty(name))
            {
                name = await connection.QueryFirstOrDefaultAsync<string>("SELECT Email FROM USERS WHERE Id = @Uid LIMIT 1", new { Uid = userId });
            }
            return Ok(name ?? userId);
        }

        [HttpPost("remove-participant")]
        public async Task<IActionResult> RemoveParticipant([FromBody] RemoveParticipantRequest request)
        {
            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                // Đảm bảo user SYSTEM tồn tại trong DB để tránh Foreign Key violation
                await connection.ExecuteAsync(@"
                    INSERT IGNORE INTO USERS (Id, Email, PublicKey, WrappedPrivateKey, Salt) VALUES ('SYSTEM', 'system@hermes.local', 'SYSTEM', 'SYSTEM', 'SYSTEM');
                    INSERT IGNORE INTO USERINFO (UserId, FullName, AvatarUrl, StatusMessage) VALUES ('SYSTEM', 'Hệ thống', '', '');
                ");

                // Lấy thông tin nhóm và tên người dùng trước khi xóa
                string userNameQuery = "SELECT FullName FROM USERINFO WHERE UserId = @Uid LIMIT 1";
                string userName = await connection.QueryFirstOrDefaultAsync<string>(userNameQuery, new { Uid = request.UserId });
                if (string.IsNullOrEmpty(userName)) userName = request.UserId;

                // Lấy danh sách tất cả các thành viên TRƯỚC KHI XÓA để báo notification
                var allParticipants = (await connection.QueryAsync<string>(
                    "SELECT UserId FROM PARTICIPANTS WHERE ConversationId = @ConvId", 
                    new { ConvId = request.ConversationId })).ToList();

                // Thực hiện xóa thành viên khỏi PARTICIPANTS
                int deleted = await connection.ExecuteAsync(
                    "DELETE FROM PARTICIPANTS WHERE ConversationId = @ConvId AND UserId = @UserId",
                    new { ConvId = request.ConversationId, UserId = request.UserId });

                if (deleted > 0)
                {
                    // Tạo tin nhắn thông báo hệ thống
                    string actionText = request.ActionType == "LEAVE" ? "đã rời khỏi nhóm" : "đã bị xóa khỏi nhóm";
                    string msgText = $"⚠️ [Thông báo] {userName} {actionText}.";

                    // Chèn tin nhắn hệ thống vào bảng MESSAGES
                    int msgId = await connection.ExecuteScalarAsync<int>(
                        "INSERT INTO MESSAGES (ConversationId, SenderId, CipherText, SentAt) VALUES (@ConvId, 'SYSTEM', @MsgText, NOW()); SELECT LAST_INSERT_ID();",
                        new { ConvId = request.ConversationId, MsgText = msgText });

                    // Chèn cho từng thành viên vào bảng MESSAGE_RECIPIENTS để GetChatHistory thấy được
                    foreach (var uid in allParticipants)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO MESSAGE_RECIPIENTS (MessageId, RecipientId, EncryptedSessionKey) VALUES (@MsgId, @RecId, '')",
                            new { MsgId = msgId, RecId = uid });
                    }

                    // Gửi SignalR tới group phòng chat
                    await _hubContext.Clients.Group(request.ConversationId.ToString()).SendAsync("ReceiveMessage", request.ConversationId.ToString(), msgText, new Dictionary<string, string>(), 0, msgId);

                    // Gửi ReceiveNewChatNotification cho toàn bộ thành viên cũ & mới để cập nhật danh sách hội thoại
                    foreach (var uid in allParticipants)
                    {
                        var conns = ChatHub.GetUserConnections(uid);
                        if (conns.Any())
                        {
                            await _hubContext.Clients.Clients(conns).SendAsync("ReceiveNewChatNotification");
                            await _hubContext.Clients.Clients(conns).SendAsync("ReceiveMessage", request.ConversationId.ToString(), msgText, new Dictionary<string, string>(), 0, msgId);
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("history/{conversationId}/{userId}")]
        public async Task<IActionResult> GetChatHistory(int conversationId, string userId)
        {
            using var connection = new MySqlConnection(ConnectionString);

            string query = @"
        SELECT m.Id as MessageId, m.SenderId, IFNULL(i.FullName, 'Hệ thống') as SenderName, m.CipherText as Content, DATE_FORMAT(m.SentAt, '%h:%i %p') as Time, mr.EncryptedSessionKey, m.TimeToLive
        FROM MESSAGES m
        LEFT JOIN USERINFO i ON m.SenderId = i.UserId
        JOIN MESSAGE_RECIPIENTS mr ON m.Id = mr.MessageId
        WHERE m.ConversationId = @ConvId AND mr.RecipientId = @UserId
        ORDER BY m.SentAt ASC";

            var history = await connection.QueryAsync(query, new { ConvId = conversationId, UserId = userId });
            return Ok(history);
        }
    }
}