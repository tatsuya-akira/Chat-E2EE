// Standardized to production level
// Purpose: Core database operations - MySQL + Firebase Realtime DB integration
// Dependencies: MySql.Data, Firebase.Database, DotNetEnv

using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hermes.Models;
using Firebase.Database;
using Firebase.Database.Query;

namespace Hermes.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = "Server=localhost;Database=hermes_db;Uid=root;Pwd=;";
        private readonly string _firebaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") ?? "https://hermes-default-rtdb.firebaseio.com/";

        private FirebaseClient CreateFirebaseClient()
        {
            string secret = Environment.GetEnvironmentVariable("FIREBASE_SECRET");
            return new FirebaseClient(_firebaseUrl, new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(secret)
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // GET USER CHATS  (No duplicates via GROUP BY c.Id)
        // ──────────────────────────────────────────────────────────────────────
        public List<ChatModel> GetUserChats(string currentUserId)
        {
            var chats = new List<ChatModel>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                // GROUP BY c.Id ensures each conversation appears exactly once.
                // For 1-on-1 chats the peer name is picked from the OTHER participant.
                string sql = @"
                    SELECT
                        c.Id AS ConversationId,
                        c.IsGroup,
                        c.GroupName,
                        (
                            SELECT ui.FullName
                            FROM participants px
                            JOIN userinfo ui ON px.UserId = ui.UserId
                            WHERE px.ConversationId = c.Id AND px.UserId != @uid
                            LIMIT 1
                        ) AS PeerName,
                        (SELECT CipherText FROM messages m WHERE m.ConversationId = c.Id ORDER BY SentAt DESC LIMIT 1) AS LastMsg,
                        (SELECT SentAt   FROM messages m WHERE m.ConversationId = c.Id ORDER BY SentAt DESC LIMIT 1) AS LastMsgTime,
                        p.LastSeenMessageId
                    FROM conversations c
                    JOIN participants p ON c.Id = p.ConversationId AND p.UserId = @uid
                    GROUP BY c.Id
                    ORDER BY LastMsgTime DESC";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", currentUserId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool isGroup = reader.GetBoolean("IsGroup");
                            string chatName = isGroup
                                ? reader["GroupName"]?.ToString()
                                : reader["PeerName"]?.ToString();
                            if (string.IsNullOrEmpty(chatName)) chatName = "Unknown Chat";

                            string lastMsg = reader["LastMsg"]?.ToString() ?? "";
                            string lastMsgTime = reader["LastMsgTime"] == DBNull.Value
                                ? ""
                                : Convert.ToDateTime(reader["LastMsgTime"]).ToString("HH:mm");

                            chats.Add(new ChatModel
                            {
                                ChatId          = reader["ConversationId"].ToString(),
                                ChatName        = chatName,
                                Initials        = chatName.Substring(0, 1).ToUpper(),
                                AvatarColor     = "#7160E8",
                                LastMessage     = lastMsg,
                                LastMessageTime = lastMsgTime
                            });
                        }
                    }
                }
            }
            return chats;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GET MESSAGES  (includes MessageId for seen-status logic)
        // ──────────────────────────────────────────────────────────────────────
        public List<MessageModel> GetMessages(long conversationId, string currentUserId)
        {
            var messages = new List<MessageModel>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                // Get the peer's LastSeenMessageId so we can mark seen ticks
                long peerLastSeen = 0;
                string seenSql = @"
                    SELECT COALESCE(MAX(LastSeenMessageId), 0)
                    FROM participants
                    WHERE ConversationId = @convId AND UserId != @uid";
                using (var cmd = new MySqlCommand(seenSql, conn))
                {
                    cmd.Parameters.AddWithValue("@convId", conversationId);
                    cmd.Parameters.AddWithValue("@uid", currentUserId);
                    var res = cmd.ExecuteScalar();
                    peerLastSeen = res == DBNull.Value ? 0L : Convert.ToInt64(res);
                }

                string sql = @"
                    SELECT m.Id AS MessageId, m.SenderId, u.FullName, m.CipherText, m.SentAt
                    FROM messages m
                    LEFT JOIN userinfo u ON m.SenderId = u.UserId
                    WHERE m.ConversationId = @convId
                    ORDER BY m.SentAt ASC";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@convId", conversationId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long msgId = Convert.ToInt64(reader["MessageId"]);
                            bool isMine = reader["SenderId"].ToString() == currentUserId;
                            messages.Add(new MessageModel
                            {
                                MessageId  = msgId,
                                SenderName = isMine ? "You" : reader["FullName"]?.ToString() ?? "?",
                                Content    = reader["CipherText"].ToString(),
                                Time       = Convert.ToDateTime(reader["SentAt"]).ToString("HH:mm"),
                                IsMine     = isMine,
                                IsSeen     = isMine && msgId <= peerLastSeen
                            });
                        }
                    }
                }
            }
            return messages;
        }

        // ──────────────────────────────────────────────────────────────────────
        // MARK CONVERSATION AS SEEN  (update LastSeenMessageId + Firebase signal)
        // ──────────────────────────────────────────────────────────────────────
        public async Task MarkSeenAsync(long conversationId, string userId)
        {
            try
            {
                // Get latest messageId in conversation
                long latestMsgId = 0L;
                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = "SELECT COALESCE(MAX(Id), 0) FROM messages WHERE ConversationId = @convId";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@convId", conversationId);
                        latestMsgId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    if (latestMsgId > 0)
                    {
                        string updateSql = @"UPDATE participants 
                                             SET LastSeenMessageId = @msgId 
                                             WHERE ConversationId = @convId AND UserId = @uid";
                        using (var cmd = new MySqlCommand(updateSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@msgId", latestMsgId);
                            cmd.Parameters.AddWithValue("@convId", conversationId);
                            cmd.Parameters.AddWithValue("@uid", userId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                // Push seen signal to Firebase so the sender sees blue tick immediately
                var firebase = CreateFirebaseClient();
                await firebase.Child("seen_sync")
                              .Child(conversationId.ToString())
                              .Child(userId)
                              .PutAsync(new { lastSeenMessageId = latestMsgId, at = DateTime.UtcNow.ToString("o") });
            }
            catch (Exception ex)
            {
                Console.WriteLine("MarkSeen Error: " + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // CREATE CONVERSATION  (dedup single chats, safe group creation)
        // ──────────────────────────────────────────────────────────────────────
        public async Task<long> CreateConversationAsync(string creatorId, List<string> participantIds, bool isGroup, string groupName = null)
        {
            if (string.IsNullOrEmpty(creatorId))
                throw new Exception("ID người dùng không hợp lệ.");

            if (participantIds == null || participantIds.Count == 0)
                throw new Exception("Danh sách người tham gia trống.");

            participantIds.RemoveAll(id => string.IsNullOrEmpty(id));
            if (!participantIds.Contains(creatorId))
                participantIds.Add(creatorId);

            // ── Dedup: for 1-on-1 chats, reuse existing conversation ──────────
            if (!isGroup && participantIds.Count == 2)
            {
                string otherId = participantIds.Find(id => id != creatorId);
                long existing = await GetExistingSingleConversationIdAsync(creatorId, otherId);
                if (existing > 0)
                    return existing; // Already exists — return without creating duplicate
            }

            long conversationId = 0;
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Insert conversation row
                        string insertConvSql = "INSERT INTO conversations (IsGroup, GroupName) VALUES (@isGroup, @groupName); SELECT LAST_INSERT_ID();";
                        using (var cmd = new MySqlCommand(insertConvSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@isGroup", isGroup);
                            cmd.Parameters.AddWithValue("@groupName", string.IsNullOrEmpty(groupName) ? (object)DBNull.Value : groupName);
                            conversationId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                        }

                        // 2. Ensure every participant exists in users/userinfo (prevent FK violations)
                        const string ensureUserSql     = "INSERT IGNORE INTO users (Id, Email) VALUES (@uId, @uEmail);";
                        const string ensureUserInfoSql = "INSERT IGNORE INTO userinfo (UserId, FullName) VALUES (@uId, @uName);";

                        using (var cmdU = new MySqlCommand(ensureUserSql, conn, transaction))
                        using (var cmdI = new MySqlCommand(ensureUserInfoSql, conn, transaction))
                        {
                            var uId1  = cmdU.Parameters.Add("@uId",   MySqlDbType.VarChar);
                            var uEmail = cmdU.Parameters.Add("@uEmail", MySqlDbType.VarChar);
                            var uId2  = cmdI.Parameters.Add("@uId",   MySqlDbType.VarChar);
                            var uName = cmdI.Parameters.Add("@uName",  MySqlDbType.VarChar);

                            foreach (var uid in participantIds)
                            {
                                uId1.Value  = uid;
                                uEmail.Value = uid + "@auto-synced.com";
                                await cmdU.ExecuteNonQueryAsync();

                                uId2.Value = uid;
                                uName.Value = "Người dùng Firebase";
                                await cmdI.ExecuteNonQueryAsync();
                            }
                        }

                        // 3. Insert participants
                        const string insertParticipantSql = "INSERT IGNORE INTO participants (ConversationId, UserId) VALUES (@convId, @userId);";
                        using (var cmd = new MySqlCommand(insertParticipantSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@convId", conversationId);
                            var uidParam = cmd.Parameters.Add("@userId", MySqlDbType.VarChar);
                            foreach (var uid in participantIds)
                            {
                                uidParam.Value = uid;
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception("Error creating conversation: " + ex.Message);
                    }
                }
            }

            // 4. Push Firebase signal to all participants
            try
            {
                var firebase = CreateFirebaseClient();
                foreach (var uid in participantIds)
                {
                    await firebase.Child("user_sync")
                                  .Child(uid)
                                  .Child(conversationId.ToString())
                                  .PutAsync(new { timestamp = DateTime.UtcNow.ToString("o"), type = "new_conversation" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase Push Error (CreateConversation): " + ex.Message);
            }

            return conversationId;
        }

        // ──────────────────────────────────────────────────────────────────────
        // PRIVATE HELPER: find existing 1-on-1 conversation
        // ──────────────────────────────────────────────────────────────────────
        private async Task<long> GetExistingSingleConversationIdAsync(string userA, string userB)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                // Find a conversation that is NOT a group AND contains both users as participants
                string sql = @"
                    SELECT c.Id
                    FROM conversations c
                    JOIN participants pa ON c.Id = pa.ConversationId AND pa.UserId = @userA
                    JOIN participants pb ON c.Id = pb.ConversationId AND pb.UserId = @userB
                    WHERE c.IsGroup = 0
                    LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userA", userA);
                    cmd.Parameters.AddWithValue("@userB", userB);
                    var res = await cmd.ExecuteScalarAsync();
                    return res == null || res == DBNull.Value ? 0L : Convert.ToInt64(res);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // SEND MESSAGE  (saves to MySQL + pushes to Firebase)
        // ──────────────────────────────────────────────────────────────────────
        public async Task<long> SendMessageAsync(long conversationId, string senderId, string cipherText, List<string> recipientIds)
        {
            long messageId = 0;
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
                            messageId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                        }

                        const string insertRecipientSql = "INSERT IGNORE INTO message_recipients (MessageId, RecipientId, EncryptedSessionKey) VALUES (@msgId, @recipientId, 'dummy_key');";
                        using (var cmd = new MySqlCommand(insertRecipientSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@msgId", messageId);
                            var rIdParam = cmd.Parameters.Add("@recipientId", MySqlDbType.VarChar);
                            foreach (var rId in recipientIds)
                            {
                                if (string.IsNullOrEmpty(rId)) continue;
                                rIdParam.Value = rId;
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception("Error sending message: " + ex.Message);
                    }
                }
            }

            try
            {
                var firebase = CreateFirebaseClient();
                await firebase.Child("conversations")
                              .Child(conversationId.ToString())
                              .Child("messages")
                              .PostAsync(new
                              {
                                  messageId = messageId,
                                  senderId  = senderId,
                                  content   = cipherText,
                                  sentAt    = DateTime.UtcNow.ToString("o")
                              });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase Push Error (SendMessage): " + ex.Message);
            }

            return messageId;
        }
        // ──────────────────────────────────────────────────────────────────────
        // GET CONVERSATION MEMBERS
        // ──────────────────────────────────────────────────────────────────────
        public List<string> GetConversationMembers(long conversationId)
        {
            var members = new List<string>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT COALESCE(ui.FullName, u.Email, p.UserId) AS DisplayName
                    FROM participants p
                    LEFT JOIN users u ON p.UserId = u.Id
                    LEFT JOIN userinfo ui ON p.UserId = ui.UserId
                    WHERE p.ConversationId = @convId";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@convId", conversationId);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            members.Add(reader["DisplayName"]?.ToString() ?? "?");
                }
            }
            return members;
        }

        // ──────────────────────────────────────────────────────────────────────
        // LEAVE CONVERSATION
        // ──────────────────────────────────────────────────────────────────────
        public async Task LeaveConversationAsync(long conversationId, string userId)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "DELETE FROM participants WHERE ConversationId = @convId AND UserId = @uid";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@convId", conversationId);
                    cmd.Parameters.AddWithValue("@uid", userId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}