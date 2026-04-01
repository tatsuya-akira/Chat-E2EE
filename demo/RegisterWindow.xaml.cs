using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace demo
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

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            string email = txtRegEmail.Text.Trim();
            string pass = txtRegPassword.Password;
            string confirm = txtConfirmPassword.Password;

            // 1. Kiểm tra rỗng
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(confirm))
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

            // Thành công
            MessageBox.Show("Đăng ký thành công!\nVui lòng kiểm tra email để xác nhận tài khoản.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            MainWindow login = new MainWindow();
            login.Show();
            this.Close();
        }

        private void TextBlock_Login_Click(object sender, MouseButtonEventArgs e)
        {
            MainWindow login = new MainWindow();
            login.Show();
            this.Close();
        }
    }
}