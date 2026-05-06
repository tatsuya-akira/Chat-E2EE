using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Firebase.Database;
using Firebase.Database.Query;

namespace Hermes.Services
{
    public class MessageService
    {
        private readonly string _connectionString = "Server=localhost;Database=hermes_db;Uid=root;Pwd=;";
        private readonly string _firebaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") ?? "https://hermes-default-rtdb.firebaseio.com/";

        public async Task<int> SendMessageAsync(int conversationId, string senderId, string cipherText, List<string> recipientIds)
        {
            int messageId = 0;
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        string insertMsgSql = "INSERT INTO messages (ConversationId, SenderId, CipherText) VALUES (@convId, @senderId, @cipherText); SELECT LAST_INSERT_ID();";
                        using (var cmd = new MySqlCommand(insertMsgSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@convId", conversationId);
                            cmd.Parameters.AddWithValue("@senderId", senderId);
                            cmd.Parameters.AddWithValue("@cipherText", cipherText);
                            messageId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        string insertRecipientSql = "INSERT INTO message_recipients (MessageId, RecipientId, EncryptedSessionKey) VALUES (@msgId, @recipientId, 'dummy_key');";
                        using (var cmd = new MySqlCommand(insertRecipientSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@msgId", messageId);
                            var recipientParam = cmd.Parameters.Add("@recipientId", MySqlDbType.VarChar);
                            foreach (var rId in recipientIds)
                            {
                                recipientParam.Value = rId;
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception("Error sending message: " + ex.Message, ex);
                    }
                }
            }

            try
            {
                var firebase = new FirebaseClient(_firebaseUrl);
                await firebase.Child("conversations")
                              .Child(conversationId.ToString())
                              .Child("messages")
                              .PostAsync(new {
                                  messageId = messageId,
                                  senderId = senderId,
                                  content = cipherText,
                                  sentAt = DateTime.UtcNow.ToString("o")
                              });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase push message failed: " + ex.Message);
            }

            return messageId;
        }
    }
}
