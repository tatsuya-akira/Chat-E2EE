using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Firebase.Database;
using Firebase.Database.Query;

namespace Hermes.Services
{
    public class ChatService
    {
        private readonly string _connectionString = "Server=localhost;Database=hermes_db;Uid=root;Pwd=;";
        private readonly string _firebaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") ?? "https://hermes-default-rtdb.firebaseio.com/";

        public async Task<int> CreateConversationAsync(string creatorId, List<string> participantIds, bool isGroup, string groupName = null)
        {
            if (participantIds == null || participantIds.Count == 0)
            {
                throw new ArgumentException("Participants cannot be null or empty.");
            }

            if (!participantIds.Contains(creatorId))
            {
                participantIds.Add(creatorId);
            }

            int conversationId = 0;

            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Insert into conversations
                        string insertConvSql = "INSERT INTO conversations (IsGroup, GroupName) VALUES (@isGroup, @groupName); SELECT LAST_INSERT_ID();";
                        using (var cmd = new MySqlCommand(insertConvSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@isGroup", isGroup);
                            cmd.Parameters.AddWithValue("@groupName", string.IsNullOrEmpty(groupName) ? DBNull.Value : (object)groupName);
                            conversationId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        // 2. Insert into participants
                        string insertParticipantSql = "INSERT INTO participants (ConversationId, UserId) VALUES (@convId, @userId);";
                        using (var cmd = new MySqlCommand(insertParticipantSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@convId", conversationId);
                            var userIdParam = cmd.Parameters.Add("@userId", MySqlDbType.VarChar);
                            foreach (var userId in participantIds)
                            {
                                userIdParam.Value = userId;
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception("Error creating conversation: " + ex.Message, ex);
                    }
                }
            }

            // 3. Push signal to Firebase RTDB for all participants
            try
            {
                var firebase = new FirebaseClient(_firebaseUrl);
                foreach (var userId in participantIds)
                {
                    await firebase.Child("user_sync")
                                  .Child(userId)
                                  .Child(conversationId.ToString())
                                  .PutAsync(new { 
                                      timestamp = DateTime.UtcNow.ToString("o"), 
                                      type = "new_conversation" 
                                  });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase push failed: " + ex.Message);
            }

            return conversationId;
        }

        public async Task SetTypingStatusAsync(int conversationId, string userId, bool isTyping)
        {
            try
            {
                var firebase = new FirebaseClient(_firebaseUrl);
                await firebase.Child("conversations")
                              .Child(conversationId.ToString())
                              .Child("typing")
                              .Child(userId)
                              .PutAsync(isTyping);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase push typing failed: " + ex.Message);
            }
        }

        public async Task SetSeenStatusAsync(int conversationId, string userId)
        {
            try
            {
                var firebase = new FirebaseClient(_firebaseUrl);
                await firebase.Child("conversations")
                              .Child(conversationId.ToString())
                              .Child("seen")
                              .Child(userId)
                              .PutAsync(DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase push seen failed: " + ex.Message);
            }
        }
    }
}
