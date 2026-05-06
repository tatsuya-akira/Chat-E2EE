using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hermes.Services;
using Xunit;

namespace Hermes.Tests
{
    public class MessageServiceTests
    {
        [Fact]
        public async Task SendMessageAsync_ValidData_ReturnsMessageId()
        {
            // Arrange
            var chatService = new ChatService();
            
            // Ensure test users exist
            using (var conn = new MySql.Data.MySqlClient.MySqlConnection("Server=localhost;Database=hermes_db;Uid=root;Pwd=;"))
            {
                await conn.OpenAsync();
                var cmd = new MySql.Data.MySqlClient.MySqlCommand("INSERT IGNORE INTO users (Id, Email, PublicKey, WrappedPrivateKey, Salt) VALUES ('test_user_1', 'test1@test.com', 'pub', 'priv', 'salt'), ('test_user_2', 'test2@test.com', 'pub', 'priv', 'salt')", conn);
                await cmd.ExecuteNonQueryAsync();
            }

            int conversationId = await chatService.CreateConversationAsync("test_user_1", new List<string> { "test_user_2" }, false);

            var messageService = new MessageService();

            // Act
            int messageId = await messageService.SendMessageAsync(conversationId, "test_user_1", "Encrypted Hello", new List<string> { "test_user_1", "test_user_2" });

            // Assert
            Assert.True(messageId > 0);
        }
    }
}
