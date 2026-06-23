using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using Hermes.Shared.DTOs;

namespace Hermes.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection") ?? "";

        public MessageController(IConfiguration configuration) { _configuration = configuration; }

        [HttpPost("save")]
        public async Task<IActionResult> SaveMessage([FromBody] SendMessageDto request)
        {
            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Lưu nội dung tin nhắn vào bảng MESSAGES
                string insertMsg = @"INSERT INTO MESSAGES (ConversationId, SenderId, CipherText, TimeToLive) 
                                     VALUES (@ConversationId, @SenderId, @CipherText, @TimeToLive); 
                                     SELECT LAST_INSERT_ID();";

                var resultObj = await connection.ExecuteScalarAsync<object>(insertMsg, request, transaction);
                int messageId = Convert.ToInt32(resultObj);

                // 2. Lưu Session Key cho từng người nhận vào bảng MESSAGE_RECIPIENTS
                string insertRecipient = @"INSERT INTO MESSAGE_RECIPIENTS (MessageId, RecipientId, EncryptedSessionKey) 
                                           VALUES (@MessageId, @RecipientId, @EncryptedKey)";

                foreach (var recipient in request.RecipientSessionKeys)
                {
                    await connection.ExecuteAsync(insertRecipient, new
                    {
                        MessageId = messageId,
                        RecipientId = recipient.Key,
                        EncryptedKey = recipient.Value
                    }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(messageId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[LỖI LƯU TIN NHẮN]: {ex.Message}");
                return StatusCode(500, "Lỗi khi lưu tin nhắn vào Database");
            }
        }
        // Lấy lịch sử tin nhắn của một cuộc trò chuyện
        [HttpGet("history/{conversationId}")]
        public async Task<IActionResult> GetChatHistory(int conversationId)
        {
            using var connection = new MySqlConnection(ConnectionString);

            // Join bảng MESSAGES và USERINFO để lấy tên người gửi
            string query = @"
        SELECT m.SenderId, i.FullName as SenderName, m.CipherText as Content, m.SentAt as Time
        FROM MESSAGES m
        JOIN USERINFO i ON m.SenderId = i.UserId
        WHERE m.ConversationId = @ConvId
        ORDER BY m.SentAt ASC";

            var history = await connection.QueryAsync(query, new { ConvId = conversationId });
            return Ok(history);
        }
    }
}