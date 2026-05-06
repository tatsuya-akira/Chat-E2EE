using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hermes.Services;
using Moq;
using Xunit;

namespace Hermes.Tests
{
    public class ChatServiceTests
    {
        [Fact]
        public async Task CreateConversationAsync_ValidInputs_ReturnsNewConversationId()
        {
            // Arrange
            var chatService = new ChatService();
            string creatorId = "test_user_1";
            var participantIds = new List<string> { "test_user_1", "test_user_2" };
            bool isGroup = false;

            // Ensure test users exist in the database to satisfy foreign keys
            using (var conn = new MySql.Data.MySqlClient.MySqlConnection("Server=localhost;Database=hermes_db;Uid=root;Pwd=;"))
            {
                await conn.OpenAsync();
                var cmd = new MySql.Data.MySqlClient.MySqlCommand("INSERT IGNORE INTO users (Id, Email, PublicKey, WrappedPrivateKey, Salt) VALUES ('test_user_1', 'test1@test.com', 'pub', 'priv', 'salt'), ('test_user_2', 'test2@test.com', 'pub', 'priv', 'salt')", conn);
                await cmd.ExecuteNonQueryAsync();
            }

            // Act
            // NOTE: This will hit the actual database since we are strictly using the hardcoded connection string.
            // Ideally, the DB should have these users. If not, the test will fail, which is expected in TDD 
            // until we set up the test data or mock the DB connection.
            var result = await chatService.CreateConversationAsync(creatorId, participantIds, isGroup);

            // Assert
            Assert.True(result > 0);
        }

        [Fact]
        public async Task CreateConversationAsync_NullParticipantIds_ThrowsArgumentException()
        {
            // Arrange
            var chatService = new ChatService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => chatService.CreateConversationAsync("test_user", null, false));
        }
    }
}
