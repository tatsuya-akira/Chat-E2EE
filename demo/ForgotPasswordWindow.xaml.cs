using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace demo
{
    public partial class ForgotPasswordWindow : Window
    {
        public ForgotPasswordWindow()
        {
            InitializeComponent();
        }

        private void btnSendCode_Click(object sender, RoutedEventArgs e)
        {
            string email = txtForgotEmail.Text.Trim();

            // 1. Kiểm tra rỗng
            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Vui lòng nhập Email để nhận mã!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Kiểm tra định dạng Email
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                MessageBox.Show("Định dạng Email không hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Thành công
            MessageBox.Show("Mã khôi phục đã được gửi vào email của bạn!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            MainWindow login = new MainWindow();
            login.Show();
            this.Close();
        }

        private void TextBlock_BackToLogin_Click(object sender, MouseButtonEventArgs e)
        {
            MainWindow login = new MainWindow();
            login.Show();
            this.Close();
        }
    }
}