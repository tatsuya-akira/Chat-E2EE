using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using Hermes.Shared.DTOs;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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
        [HttpGet("history/{conversationId}/{userId}")]
        public async Task<IActionResult> GetChatHistory(int conversationId, string userId)
        {
            using var connection = new MySqlConnection(ConnectionString);

            // Dùng DATE_FORMAT để ép MySQL trả về chữ (VD: 09:15 AM) thay vì kiểu DateTime, tránh lỗi cho C#
            string query = @"
                SELECT m.Id as MessageId, m.SenderId, IFNULL(i.FullName, 'Hệ thống') as SenderName, m.CipherText as Content, 
                       DATE_FORMAT(m.SentAt, '%h:%i %p') as Time, 
                       mr.EncryptedSessionKey, mr.IsRead, m.TimeToLive
                FROM MESSAGES m
                LEFT JOIN USERINFO i ON m.SenderId = i.UserId
                JOIN MESSAGE_RECIPIENTS mr ON m.Id = mr.MessageId
                WHERE m.ConversationId = @ConvId AND mr.RecipientId = @UserId
                ORDER BY m.SentAt ASC";

            var history = await connection.QueryAsync(query, new { ConvId = conversationId, UserId = userId });
            return Ok(history);
        }

        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            using var connection = new MySqlConnection(ConnectionString);
            await connection.ExecuteAsync("DELETE FROM MESSAGE_RECIPIENTS WHERE MessageId = @Id", new { Id = messageId });
            await connection.ExecuteAsync("DELETE FROM MESSAGES WHERE Id = @Id", new { Id = messageId });
            return Ok();
        }
        [HttpPut("mark-read/{conversationId}/{readerId}")]
        public async Task<IActionResult> MarkAsRead(int conversationId, string readerId)
        {
            using var connection = new MySqlConnection(ConnectionString);
            string query = @"
        UPDATE MESSAGE_RECIPIENTS mr
        JOIN MESSAGES m ON mr.MessageId = m.Id
        SET mr.IsRead = 1, mr.ReadAt = CURRENT_TIMESTAMP
        WHERE m.ConversationId = @ConvId AND mr.RecipientId = @ReaderId AND mr.IsRead = 0";

            await connection.ExecuteAsync(query, new { ConvId = conversationId, ReaderId = readerId });
            return Ok();
        }
    }
}