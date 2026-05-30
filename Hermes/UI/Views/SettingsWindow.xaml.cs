using System;
using System.Linq;
using System.Windows;

namespace Hermes
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadUserData();
        }

        private void LoadUserData()
        {
            txtUserId.Text = "UID: " + (AuthService.CurrentUserId ?? "N/A");
            txtUserEmail.Text = "Email: " + (Environment.GetEnvironmentVariable("USER_EMAIL") ?? "Tài khoản hiện tại");
            txtUserName.Text = "Tên người dùng: " + (AuthService.CurrentFullName ?? "N/A");
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                Backend.Services.CryptoService.ClearPrivateKeyLocal();

                AuthService.Logout();
                MainWindow login = new MainWindow();
                login.Show();

                // Đóng tất cả cửa sổ hiện tại ngoại trừ cửa sổ login
                var windowsToClose = System.Windows.Application.Current.Windows.OfType<Window>().ToList();
                foreach (var w in windowsToClose)
                {
                    if (w != login)
                    {
                        w.Close();
                    }
                }
            }
        }
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            string myPrivateKey = AuthService.CurrentPrivateKey;
            MessageBox.Show($"ĐÂY LÀ KHÓA BÍ MẬT RSA CỦA BẠN:\n\n{myPrivateKey}", "Khóa cá nhân", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}