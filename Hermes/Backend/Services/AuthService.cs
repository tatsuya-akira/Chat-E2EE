using Firebase.Auth;
using Hermes.Backend.Services;
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
        public static string CurrentPrivateKey { get; private set; }
        public static string CurrentPublicKey { get; private set; }

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
                    await LoadUserKeysAsync(CurrentUserId, password);
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
                if (await ApiClient.CheckIdentifierExistsAsync(username))
                {
                    throw new Exception("Tên hiển thị (Username) đã tồn tại, vui lòng chọn tên khác!");
                }
                if (await ApiClient.CheckIdentifierExistsAsync(email))
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
                throw new Exception("Lỗi kết nối Server API khi kiểm tra tài khoản.");
            }

            try
            {
                var auth = await _authProvider.CreateUserWithEmailAndPasswordAsync(email, password);

                if (auth != null && !string.IsNullOrEmpty(auth.User.LocalId))
                {
                    var salt = CryptoService.GenerateSalt();
                    var masterKey = CryptoService.DeriveMasterKey(password, salt);
                    var keys = CryptoService.GenerateRSAKeys();
                    var wrappedPriv = CryptoService.EncryptPrivateKey(keys.PrivateKeyBase64, masterKey);

                    var request = new Hermes.Shared.DTOs.RegisterRequest
                    {
                        Id = auth.User.LocalId,
                        Email = email,
                        FullName = username,
                        PublicKey = keys.PublicKeyBase64,
                        WrappedPrivateKey = wrappedPriv,
                        Salt = salt
                    };

                    bool isSaved = await ApiClient.RegisterUserAsync(request);
                    if (!isSaved)
                    {
                        // Rollback Firebase
                        if (!string.IsNullOrEmpty(auth.FirebaseToken))
                        {
                            try { await _authProvider.DeleteUserAsync(auth.FirebaseToken); } catch { }
                        }
                        throw new Exception("Lỗi Server khi lưu thông tin người dùng.");
                    }
                    return true;
                }
                return false;
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
            CurrentPrivateKey = null;
            CurrentPublicKey = null;
        }

        public static string GetUsernameByIdentifier(string identifier)
        {
            // Now managed by API, this method is left for backward compatibility in WPF UI locally if needed,
            // or replace it directly async
            return null; // temporary stub
        }

        private static async Task LoadUserKeysAsync(string userId, string password)
        {
            var keys = await ApiClient.GetUserKeysAsync(userId);
            if (keys != null)
            {
                CurrentPublicKey = keys.PublicKey;
                var masterKey = CryptoService.DeriveMasterKey(password, keys.Salt);
                CurrentPrivateKey = CryptoService.DecryptPrivateKey(keys.WrappedPrivateKey, masterKey);
            }
        }
    }
}
