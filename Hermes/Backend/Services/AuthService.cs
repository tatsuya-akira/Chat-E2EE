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
        public static string CurrentPrivateKey { get; set; }
        public static string CurrentPublicKey { get; private set; }
        public static string CurrentFullName { get; set; }
        public static string CurrentEmail { get; set; }   // Email đăng nhập hiện tại

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
                    var userInfo = await ApiClient.GetUserByIdentifierAsync(email);
                    CurrentFullName = userInfo?.FullName ?? "Người dùng ẩn danh";
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

        //public static async Task<string> GetUsernameByIdentifierAsync(string identifier)
        //{
        //    try
        //    {
        //        // Gọi API lên Server để tìm user
        //        var userInfo = await Backend.Services.ApiClient.GetUserByIdentifierAsync(identifier);

        //        // Nếu tìm thấy thì trả về tên, không thì trả về "Ẩn danh"
        //        return userInfo?.FullName ?? "Người dùng ẩn danh";
        //    }
        //    catch
        //    {
        //        return "Lỗi kết nối";
        //    }
        //}

        private static async Task LoadUserKeysAsync(string userId, string password)
        {
            var keys = await ApiClient.GetUserKeysAsync(userId);
            if (keys != null)
            {
                CurrentPublicKey = keys.PublicKey;
                var masterKey = CryptoService.DeriveMasterKey(password, keys.Salt);
                CurrentPrivateKey = CryptoService.DecryptPrivateKey(keys.WrappedPrivateKey, masterKey);

                // --- BẮT LỖI SAI CHÌA KHÓA E2EE ---
                if (CurrentPrivateKey != null && CurrentPrivateKey.StartsWith("[Lỗi:"))
                {
                    CurrentPrivateKey = null;
                    throw new Exception("E2EE_KEY_CORRUPTED");
                }

                CryptoService.SavePrivateKeyLocal(CurrentPrivateKey);
            }
        }
        public static async Task<bool> ResetAccountKeysAsync(string newPassword)
        {
            try
            {
                var newSalt = CryptoService.GenerateSalt();
                var newMasterKey = CryptoService.DeriveMasterKey(newPassword, newSalt);
                var newKeys = CryptoService.GenerateRSAKeys();
                var newWrappedPriv = CryptoService.EncryptPrivateKey(newKeys.PrivateKeyBase64, newMasterKey);

                var request = new Hermes.Shared.DTOs.UpdateKeyRequest
                {
                    UserId = CurrentUserId,
                    PublicKey = newKeys.PublicKeyBase64,
                    WrappedPrivateKey = newWrappedPriv,
                    Salt = newSalt
                };

                bool isUpdated = await ApiClient.UpdateUserKeysAsync(request);

                if (isUpdated)
                {
                    CurrentPublicKey = newKeys.PublicKeyBase64;
                    CurrentPrivateKey = newKeys.PrivateKeyBase64;
                    CryptoService.SavePrivateKeyLocal(CurrentPrivateKey);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi khôi phục khóa: " + ex.Message);
            }
        }
        // 1. ĐĂNG NHẬP / ĐĂNG KÝ BẰNG GOOGLE
        public static async Task<bool> LoginWithGoogleAsync(string googleIdToken, string e2eePinCode)
        {
            try
            {
                // Gửi Token của Google cho Firebase để đổi lấy Firebase Token
                var auth = await _authProvider.SignInWithOAuthAsync(FirebaseAuthType.Google, googleIdToken);

                if (auth != null && !string.IsNullOrEmpty(auth.FirebaseToken))
                {
                    CurrentUserId = auth.User.LocalId;
                    CurrentToken = auth.FirebaseToken;

                    // Kiểm tra xem User này đã có trong MySQL chưa
                    var userInfo = await ApiClient.GetUserByIdentifierAsync(auth.User.Email);

                    if (userInfo == null)
                    {
                        // TÀI KHOẢN MỚI: Tự động Đăng ký và tạo khóa E2EE bằng mã PIN
                        await RegisterThirdPartyUserAsync(auth.User, e2eePinCode);
                        CurrentFullName = auth.User.DisplayName ?? "Người dùng Google";
                    }
                    else
                    {
                        // TÀI KHOẢN CŨ: Dùng mã PIN để mở khóa E2EE
                        await LoadUserKeysAsync(CurrentUserId, e2eePinCode);
                        CurrentFullName = userInfo.FullName;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đăng nhập Google: {ex.Message}");
            }
        }

        // 2. ĐĂNG NHẬP / ĐĂNG KÝ BẰNG SỐ ĐIỆN THOẠI (SMS OTP)
        // Hàm 1: Gửi mã OTP (Yêu cầu Server Node.js/C# hỗ trợ Firebase Admin hoặc Twilio)
        public static async Task<string> RequestPhoneOTPAsync(string phoneNumber)
        {
            // Trên WPF, Firebase bắt buộc phải có reCAPTCHA. 
            // Do đó, ta sẽ gọi một API nội bộ của Server Hermes để Server tự bắn SMS
            // string sessionId = await ApiClient.SendSmsOtpAsync(phoneNumber);
            // return sessionId;

            throw new NotImplementedException("Cần tích hợp API Twilio/SpeedSMS ở Backend");
        }

        // Hàm 2: Xác thực mã OTP người dùng nhập vào
        public static async Task<bool> VerifyPhoneOTPAndLoginAsync(string sessionId, string otpCode, string e2eePinCode)
        {
            // var auth = await ApiClient.VerifySmsOtpAsync(sessionId, otpCode);
            // Xử lý tương tự như LoginWithGoogleAsync ở trên (Check MySQL -> Tạo khóa bằng PIN)
            throw new NotImplementedException("Cần tích hợp API xác thực OTP ở Backend");
        }

        // HÀM HỖ TRỢ: Đăng ký ngầm cho tài khoản bên thứ 3
        private static async Task RegisterThirdPartyUserAsync(Firebase.Auth.User user, string e2eePinCode)
        {
            var salt = CryptoService.GenerateSalt();
            // Dùng mã PIN (thay vì mật khẩu) để khóa Private Key
            var masterKey = CryptoService.DeriveMasterKey(e2eePinCode, salt);
            var keys = CryptoService.GenerateRSAKeys();
            var wrappedPriv = CryptoService.EncryptPrivateKey(keys.PrivateKeyBase64, masterKey);

            var request = new Hermes.Shared.DTOs.RegisterRequest
            {
                Id = user.LocalId,
                Email = user.Email ?? $"{user.LocalId}@phone.hermes", // Fake email cho đăng nhập bằng SĐT
                FullName = user.DisplayName ?? "Người dùng mới",
                PublicKey = keys.PublicKeyBase64,
                WrappedPrivateKey = wrappedPriv,
                Salt = salt
            };

            bool isSaved = await ApiClient.RegisterUserAsync(request);
            if (!isSaved) throw new Exception("Lỗi Server khi khởi tạo hòm thư E2EE.");

            CurrentPublicKey = keys.PublicKeyBase64;
            CurrentPrivateKey = keys.PrivateKeyBase64;
            CryptoService.SavePrivateKeyLocal(CurrentPrivateKey);
        }
    }
}
