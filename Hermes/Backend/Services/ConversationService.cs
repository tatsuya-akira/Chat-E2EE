using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Hermes.Backend.Services
{
    public static class ConversationService
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING") ?? "DB";

        public static (string UserId, string FullName) GetUserByIdentifier(string identifier)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                string query = @"
                    SELECT u.Id, i.FullName FROM USERS u
                    JOIN USERINFO i ON u.Id = i.UserId
                    WHERE u.Email = @Iden OR i.FullName = @Iden LIMIT 1";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Iden", identifier);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (reader["Id"].ToString(), reader["FullName"].ToString());
                        }
                    }
                }
            }
            return (null, null);
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
                        int conversationId = 0;
                        string insertConv = "INSERT INTO CONVERSATIONS (IsGroup, GroupName) VALUES (@IsGroup, @GroupName); SELECT LAST_INSERT_ID();";
                        using (var cmd = new MySqlCommand(insertConv, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@IsGroup", isGroup);
                            cmd.Parameters.AddWithValue("@GroupName", (object)groupName ?? DBNull.Value);
                            conversationId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        string insertParticipant = "INSERT INTO PARTICIPANTS (ConversationId, UserId) VALUES (@ConvId, @UserId)";
                        foreach (var uid in userIds)
                        {
                            using (var cmd = new MySqlCommand(insertParticipant, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConvId", conversationId);
                                cmd.Parameters.AddWithValue("@UserId", uid);
                                cmd.ExecuteNonQuery();
                            }
                        }

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
