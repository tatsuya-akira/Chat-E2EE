using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Dapper;

namespace Hermes.Backend.Services
{
    public static class ConversationService
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING") ?? "DB";

        public static async Task<(string UserId, string FullName)> GetUserByIdentifierAsync(string identifier)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                string query = @"
                    SELECT u.Id, i.FullName FROM USERS u
                    JOIN USERINFO i ON u.Id = i.UserId
                    WHERE u.Email = @Iden OR i.FullName = @Iden LIMIT 1";

                var user = await connection.QueryFirstOrDefaultAsync<(string Id, string FullName)>(query, new { Iden = identifier });
                return user;
            }
        }

        public static (string UserId, string FullName) GetUserByIdentifier(string identifier)
        {
            return GetUserByIdentifierAsync(identifier).GetAwaiter().GetResult();
        }

        public static int CreateConversation(bool isGroup, string groupName, List<string> userIds)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string insertConv = "INSERT INTO CONVERSATIONS (IsGroup, GroupName) VALUES (@IsGroup, @GroupName); SELECT LAST_INSERT_ID();";
                        int conversationId = connection.ExecuteScalar<int>(insertConv, new { IsGroup = isGroup, GroupName = groupName }, transaction);

                        string insertParticipant = "INSERT INTO PARTICIPANTS (ConversationId, UserId) VALUES (@ConvId, @UserId)";
                        var participants = userIds.Select(uid => new { ConvId = conversationId, UserId = uid }).ToList();

                        connection.Execute(insertParticipant, participants, transaction);

                        transaction.Commit();
                        return conversationId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
