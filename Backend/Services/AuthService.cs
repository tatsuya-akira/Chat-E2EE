using Firebase.Auth;
using Hermes.Services;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace Hermes
{
    public static class AuthService
    {
        private static readonly string ApiKey;
        private static readonly string AuthDomain;
        private static string MySqlConnectionString = "Server=localhost;Database=hermes_db;Uid=root;Pwd=;";

        private static FirebaseAuthProvider _authProvider;
        public static string CurrentUserId { get; set; }
        public static string CurrentToken { get; private set; }

        static AuthService()
        {
            DotNetEnv.Env.TraversePath().Load();

            ApiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ?? "AIzaSy...";
            AuthDomain = Environment.GetEnvironmentVariable("FIREBASE_AUTH_DOMAIN") ?? "hermes-chat-uit.firebaseapp.com";

            _authProvider = new FirebaseAuthProvider(new FirebaseConfig(ApiKey));
        }


        public static string GetUserIdByIdentifier(string identifier)
        {
            string userId = null;
            try
            {
                using (var conn = new MySqlConnection(MySqlConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT u.Id 
                        FROM users u 
                        LEFT JOIN userinfo i ON u.Id = i.UserId 
                        WHERE u.Email = @identifier OR i.FullName = @identifier 
                        LIMIT 1";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@identifier", identifier);
                        var result = cmd.ExecuteScalar();
                        userId = result?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Lỗi Database: " + ex.Message);
            }
            return userId;
        }

        public static string GetUsernameByIdentifier(string identifier)
        {
            try
            {
                using (var connection = new MySqlConnection(MySqlConnectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT i.FullName FROM users u
                        JOIN userinfo i ON u.Id = i.UserId
                        WHERE u.Email = @Iden OR i.FullName = @Iden LIMIT 1";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Iden", identifier);
                        return cmd.ExecuteScalar()?.ToString();
                    }
                }
            }
            catch { return null; }
        }

        public static void SaveUserToDatabase(string userId, string email, string username)
        {
            try
            {
                using (var connection = new MySqlConnection(MySqlConnectionString))
                {
                    connection.Open();
                    string insertUserQuery = "INSERT IGNORE INTO users (Id, Email) VALUES (@Id, @Email)";
                    using (var cmd = new MySqlCommand(insertUserQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", userId);
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.ExecuteNonQuery();
                    }

                    string insertInfoQuery = "INSERT IGNORE INTO userinfo (UserId, FullName) VALUES (@UserId, @FullName)";
                    using (var cmd = new MySqlCommand(insertInfoQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@FullName", username);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error syncing user to DB: " + ex.Message);
            }
        }


        public static async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                var auth = await _authProvider.SignInWithEmailAndPasswordAsync(email, password);
                if (auth != null && !string.IsNullOrEmpty(auth.FirebaseToken))
                {
                    CurrentUserId = auth.User.LocalId;
                    CurrentToken = auth.FirebaseToken;
                    Console.WriteLine("Logged in UserID: " + CurrentUserId);
                    
                    string fallbackUsername = email.Contains("@") ? email.Split('@')[0] : email;
                    SaveUserToDatabase(CurrentUserId, email, fallbackUsername);

                    return true;
                }
                return false;
            }
            catch (FirebaseAuthException ex)
            {
                if (ex.Reason == AuthErrorReason.WrongPassword) throw new Exception("Mật khẩu không chính xác.");
                if (ex.Reason == AuthErrorReason.UnknownEmailAddress) throw new Exception("Tài khoản email không tồn tại.");
                throw new Exception("Đăng nhập thất bại.");
            }
        }

        public static async Task<bool> RegisterAsync(string email, string password, string username)
        {
            try
            {
                if (GetUsernameByIdentifier(username) != null) throw new Exception("Tên hiển thị đã tồn tại!");
                if (GetUsernameByIdentifier(email) != null) throw new Exception("Email đã tồn tại!");

                var auth = await _authProvider.CreateUserWithEmailAndPasswordAsync(email, password);
                if (auth != null && !string.IsNullOrEmpty(auth.User.LocalId))
                {
                    SaveUserToDatabase(auth.User.LocalId, email, username);
                    return true;
                }
                return false;
            }
            catch (FirebaseAuthException ex)
            {
                if (ex.Reason == AuthErrorReason.EmailExists) throw new Exception("Email đã tồn tại trên hệ thống.");
                throw new Exception("Đăng ký thất bại.");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public static async Task<bool> SendPasswordResetEmailAsync(string email)
        {
            await _authProvider.SendPasswordResetEmailAsync(email);
            return true;
        }

        public static void Logout()
        {
            CurrentUserId = null;
            CurrentToken = null;
        }
    }
}