// Standardized to production level
// Purpose: Login window – email/password + Google OAuth, E2EE key bootstrap
// Dependencies: AuthService, CryptoService, Google.Apis.Auth.OAuth2
using Microsoft.VisualBasic;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // KIỂM TRA: Dưới ổ cứng có chìa khóa không?
            string savedKey = Backend.Services.CryptoService.LoadPrivateKeyLocal();
            if (!string.IsNullOrEmpty(savedKey))
            {
                // Có khóa! Nạp lên RAM luôn
                AuthService.CurrentPrivateKey = savedKey;
            }
        }

        private bool IsValidEmail(string email)
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password;

            try
            {
                // 1. Kiểm tra rỗng
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Vui lòng nhập đầy đủ Email và Mật khẩu!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Kiểm tra định dạng Email
                if (!IsValidEmail(email))
                {
                    MessageBox.Show("Định dạng Email không hợp lệ! (Ví dụ: abc@gmail.com)", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Kiểm tra giới hạn ký tự mật khẩu
                if (password.Length < 6 || password.Length > 20)
                {
                    MessageBox.Show("Mật khẩu phải từ 6 đến 20 ký tự!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnLogin.IsEnabled = false;
                bool res = await AuthService.LoginAsync(email, password);
                if (res)
                {
                    // Lưu email vào session để Settings hiển thị đúng
                    AuthService.CurrentEmail = email;
                    ChatWindow chat = new ChatWindow();
                    chat.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                // BẮT MÃ LỖI ĐẶC BIỆT KHI SAI CHÌA KHÓA E2EE
                if (ex.Message.Contains("E2EE_KEY_CORRUPTED"))
                {
                    var res = MessageBox.Show(
                        "Bạn đang đăng nhập bằng mật khẩu mới nhưng hệ thống mã hóa E2EE từ chối giải mã.\n\n" +
                        "Bạn có muốn ĐẶT LẠI TÀI KHOẢN? (Hệ thống sẽ tạo khóa bảo mật mới cho bạn, nhưng TOÀN BỘ lịch sử chat cũ sẽ không thể đọc được nữa).",
                        "Cảnh báo bảo mật E2EE",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            await AuthService.ResetAccountKeysAsync(password);
                            MessageBox.Show("Khôi phục tài khoản thành công! Khóa bảo mật mới đã được tạo.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                            ChatWindow chat = new ChatWindow();
                            chat.Show();
                            this.Close();
                        }
                        catch (Exception resetEx)
                        {
                            MessageBox.Show(resetEx.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                            btnLogin.IsEnabled = true;
                        }
                    }
                    else
                    {
                        btnLogin.IsEnabled = true;
                    }
                }
                else
                {
                    MessageBox.Show(ex.Message, "Lỗi đăng nhập", MessageBoxButton.OK, MessageBoxImage.Error);
                    btnLogin.IsEnabled = true;
                }
            }
        }

        private void TextBlock_Register_Click(object sender, MouseButtonEventArgs e)
        {
            RegisterWindow reg = new RegisterWindow();
            reg.Show();
            this.Close();
        }

        private void TextBlock_ForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {
            ForgotPasswordWindow forgot = new ForgotPasswordWindow();
            forgot.Show();
            this.Close();
        }

        public async Task<string> GetGoogleIdTokenAsync()
        {
            DotNetEnv.Env.TraversePath().Load();

            string clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "GOOGLE_CLIENT_ID";
            string clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "GOOGLE_CLIENT_SECRET";
            string[] scopes = { "openid", "email", "profile" };

            try
            {
                ClientSecrets secrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    scopes,
                    "user_hermes",
                    CancellationToken.None,
                    new Google.Apis.Util.Store.FileDataStore("Hermes.GoogleAuth")
                );

                if (credential.Token.IsExpired(credential.Flow.Clock))
                {
                    await credential.RefreshTokenAsync(CancellationToken.None);
                }

                return credential.Token.AccessToken;
            }
            catch (Exception ex)
            {
                throw new Exception("Hủy đăng nhập hoặc có lỗi xảy ra: " + ex.Message);
            }
        }

        private async void btnGoogleLogin_Click(object sender, RoutedEventArgs e)
        {
            string e2eePinCode = "";

            try
            {
                string idToken = await GetGoogleIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return;

                e2eePinCode = Microsoft.VisualBasic.Interaction.InputBox(
                    "Vui lòng nhập mã PIN bảo mật E2EE (Tạo mới nếu đăng nhập lần đầu):",
                    "Bảo mật Hòm thư"
                );

                if (string.IsNullOrEmpty(e2eePinCode) || e2eePinCode.Length < 4)
                {
                    MessageBox.Show("Mã PIN phải từ 4 ký tự trở lên!");
                    return;
                }

                bool res = await AuthService.LoginWithGoogleAsync(idToken, e2eePinCode);
                if (res)
                {
                    // Ghi nhận loại tài khoản Google cho Settings
                    if (string.IsNullOrEmpty(AuthService.CurrentEmail))
                        AuthService.CurrentEmail = "(Google Account)";
                    ChatWindow chat = new ChatWindow();
                    chat.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                // XỬ LÝ ÊM ÁI LỖI SAI MÃ PIN
                if (ex.Message.Contains("E2EE_KEY_CORRUPTED"))
                {
                    var res = MessageBox.Show(
                        "Mã PIN bảo mật E2EE không chính xác. Tài khoản cũ của bạn đang được mã hóa bằng một mật khẩu khác.\n\n" +
                        "Bạn có muốn ĐẶT LẠI TÀI KHOẢN? (Hệ thống sẽ tạo khóa bảo mật mới bằng mã PIN bạn vừa nhập, nhưng lịch sử chat cũ sẽ không thể đọc được).",
                        "Cảnh báo bảo mật E2EE",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            await AuthService.ResetAccountKeysAsync(e2eePinCode);
                            MessageBox.Show("Khôi phục tài khoản thành công! Khóa bảo mật mới đã được tạo.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                            ChatWindow chat = new ChatWindow();
                            chat.Show();
                            this.Close();
                        }
                        catch (Exception resetEx)
                        {
                            MessageBox.Show("Lỗi khi khôi phục khóa: " + resetEx.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(ex.Message, "Lỗi đăng nhập Google", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}