using Firebase.Auth;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace Hermes
{
    public static class AuthService
    {
        private static readonly string ApiKey;
        private static readonly string AuthDomain;
        private static readonly string MySqlConnectionString;

        private static FirebaseAuthProvider _authProvider;
        public static string CurrentUserId { get; private set; }
        public static string CurrentToken { get; private set; }

        static AuthService()
        {
            DotNetEnv.Env.TraversePath().Load();

            ApiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ?? "API_KEY";
            AuthDomain = Environment.GetEnvironmentVariable("FIREBASE_AUTH_DOMAIN") ?? "AUTH_DOMAIN";
            MySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING") ?? "DB";

            _authProvider = new FirebaseAuthProvider(new FirebaseConfig(ApiKey));
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
                    return true;
                }
                return false;
            }
            catch (FirebaseAuthException ex)
            {
                if (ex.Reason == AuthErrorReason.WrongPassword)
                {
                    throw new Exception("Mật khẩu không chính xác.");
                }
                else if (ex.Reason == AuthErrorReason.UnknownEmailAddress)
                {
                    throw new Exception("Tài khoản email không tồn tại.");
                }
                throw new Exception("Đăng nhập thất bại. Vui lòng thử lại.");
            }
        }

        public static async Task<bool> RegisterAsync(string email, string password, string username)
        {
            try
            {
                // Kiểm tra Username hoặc Email đã tồn tại trong DB chưa
                if (GetUsernameByIdentifier(username) != null)
                {
                    throw new Exception("Tên hiển thị (Username) đã tồn tại, vui lòng chọn tên khác!");
                }
                if (GetUsernameByIdentifier(email) != null)
                {
                    throw new Exception("Email đã tồn tại trong hệ thống!");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("tồn tại"))
            {
                throw;
            }
            catch
            {
                throw new Exception("Lỗi kết nối cơ sở dữ liệu khi kiểm tra tài khoản.");
            }

            try
            {
                var auth = await _authProvider.CreateUserWithEmailAndPasswordAsync(email, password);

                try
                {
                    if (auth != null && !string.IsNullOrEmpty(auth.User.LocalId))
                    {
                        SaveUserToDatabase(auth.User.LocalId, email, username);
                        return true;
                    }
                    return false;
                }
                catch (Exception)
                {
                    // Xóa tài khoản trên Firebase nếu lưu vào MySQL thất bại để tránh bất đồng bộ
                    if (auth != null && !string.IsNullOrEmpty(auth.FirebaseToken))
                    {
                        try { await _authProvider.DeleteUserAsync(auth.FirebaseToken); } catch { }
                    }
                    throw new Exception("Lỗi hệ thống khi lưu thông tin. Đã hoàn tác việc tạo tài khoản, vui lòng thử lại sau.");
                }
            }
            catch (FirebaseAuthException ex)
            {
                if (ex.Reason == AuthErrorReason.EmailExists)
                {
                    throw new Exception("Tài khoản email này đã tồn tại trên Firebase.");
                }
                throw new Exception("Đăng ký thất bại. Vui lòng thử lại.");
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

        public static string GetUsernameByIdentifier(string identifier)
        {
            using (var connection = new MySqlConnection(MySqlConnectionString))
            {
                connection.Open();
                string query = @"
                    SELECT i.FullName FROM Users u
                    JOIN Info i ON u.Id = i.UserId
                    WHERE u.Email = @Iden OR i.FullName = @Iden LIMIT 1";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Iden", identifier);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        private static void SaveUserToDatabase(string userId, string email, string username)
        {
            using (var connection = new MySqlConnection(MySqlConnectionString))
            {
                connection.Open();

                // Lưu vào bảng Users
                string insertUserQuery = "INSERT INTO Users (Id, Email) VALUES (@Id, @Email)";
                using (var cmd = new MySqlCommand(insertUserQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.ExecuteNonQuery();
                }

                // Lưu vào bảng Info (liên kết khóa ngoại)
                string insertInfoQuery = "INSERT INTO Info (UserId, FullName) VALUES (@UserId, @FullName)";
                using (var cmd = new MySqlCommand(insertInfoQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@FullName", username);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
