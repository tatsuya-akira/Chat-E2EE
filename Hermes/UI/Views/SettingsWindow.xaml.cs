using System;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

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

        // ĐỔI THÀNH async void ĐỂ DÙNG ĐƯỢC await
        private async void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                // 1. Xóa khóa E2EE cục bộ
                Backend.Services.CryptoService.ClearPrivateKeyLocal();

                // 2. Đăng xuất khỏi hệ thống hiện tại của bạn
                AuthService.Logout();

                // 3. XÓA CACHE ĐĂNG NHẬP CỦA GOOGLE
                try
                {
                    // Tên thư mục này phải giống y hệt lúc bạn gọi FileDataStore ở màn hình Login
                    var dataStore = new Google.Apis.Util.Store.FileDataStore("Hermes.GoogleAuth");
                    await dataStore.ClearAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi xóa cache Google: " + ex.Message);
                }

                // 4. Mở lại màn hình Đăng nhập
                MainWindow login = new MainWindow();
                login.Show();

                // 5. Đóng tất cả các cửa sổ khác
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