using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace Hermes
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private bool IsValidEmail(string email)
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private async void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = txtUsername.Text.Trim();
                string email = txtRegEmail.Text.Trim();
                string pass = txtRegPassword.Password;
                string confirm = txtConfirmPassword.Password;

                // 1. Kiểm tra rỗng
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(confirm))
                {
                    MessageBox.Show("Vui lòng điền đầy đủ tất cả các ô!", "Lỗi đăng ký", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Kiểm tra định dạng Email
                if (!IsValidEmail(email))
                {
                    MessageBox.Show("Định dạng Email không hợp lệ!", "Lỗi đăng ký", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Kiểm tra độ dài mật khẩu
                if (pass.Length < 6 || pass.Length > 20)
                {
                    MessageBox.Show("Mật khẩu phải từ 6 đến 20 ký tự!", "Lỗi đăng ký", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 4. Kiểm tra mật khẩu xác nhận có khớp không
                if (pass != confirm)
                {
                    MessageBox.Show("Mật khẩu xác nhận không khớp!", "Lỗi đăng ký", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Đăng ký qua Firebase và lưu vào MySQL
                btnRegister.IsEnabled = false; // Disable nút đăng ký khi đang gọi DB/API
                bool res = await AuthService.RegisterAsync(email, pass, username);
                if (res)
                {
                    // Thành công
                    MessageBox.Show("Đăng ký thành công!\nVui lòng kiểm tra email để xác nhận tài khoản nếu có yêu cầu hoặc đăng nhập.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    MainWindow login = new MainWindow();
                    login.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đăng ký: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                btnRegister.IsEnabled = true;
            }
        }

        private void TextBlock_Login_Click(object sender, MouseButtonEventArgs e)
        {
            MainWindow login = new MainWindow();
            login.Show();
            this.Close();
        }
    }
}