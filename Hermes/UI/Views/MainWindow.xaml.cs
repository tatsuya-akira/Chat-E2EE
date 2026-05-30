using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

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

                // Lưu ý: Tùy kiến trúc của bạn, bạn cần lưu thêm ID hoặc Email user vào setting cục bộ
                // để API biết là ai đang login. Giả sử bạn lưu ID thành công:
                // AuthService.CurrentUserId = "..."; 

                //ChatWindow chat = new ChatWindow();
                //chat.Show();
                //this.Close();
            }
        }

        private bool IsValidEmail(string email)
        {
            // Kiểm tra định dạng có chữ @ và dấu chấm
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

                // Nếu qua hết các bài kiểm tra thì mới cho vào
                btnLogin.IsEnabled = false; // Disable nút đăng nhập khi đang gọi API
                bool res = await AuthService.LoginAsync(email, password);
                if (res)
                {
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
    }
}