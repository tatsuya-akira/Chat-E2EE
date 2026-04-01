using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace demo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private bool IsValidEmail(string email)
        {
            // Kiểm tra định dạng có chữ @ và dấu chấm
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password;

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
            ChatWindow chat = new ChatWindow();
            chat.Show();
            this.Close();
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