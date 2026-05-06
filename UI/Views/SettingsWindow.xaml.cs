// Standardized to production level
// Purpose: SettingsWindow code-behind — Avatar upload, Profile edit, Online status, Security
// Dependencies: DatabaseService, AuthService, FirebaseStorage.net

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Firebase.Storage;
using Hermes.Models;
using Hermes.Services;
using Microsoft.Win32;

namespace Hermes
{
    public partial class SettingsWindow : Window
    {
        // ── Fields ──────────────────────────────────────────────────────────────
        private readonly DatabaseService _dbService = new();
        private UserProfileModel _profile = new();
        private string _localAvatarPath = string.Empty; // temp path before save

        // ── Constructor ──────────────────────────────────────────────────────────
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = _profile;
            Loaded += async (_, _) => await LoadProfileAsync();
        }

        // ── LOAD PROFILE ─────────────────────────────────────────────────────────
        private async Task LoadProfileAsync()
        {
            string userId = AuthService.CurrentUserId ?? string.Empty;
            if (string.IsNullOrEmpty(userId)) return;

            var data = await Task.Run(() => _dbService.GetUserProfile(userId));
            if (data == null)
            {
                // Minimal fallback from AuthService
                _profile.UserId = userId;
                _profile.Email  = string.Empty;
            }
            else
            {
                _profile.UserId      = data.UserId;
                _profile.Email       = data.Email;
                _profile.Username    = data.Username;
                _profile.DisplayName = data.DisplayName;
                _profile.Bio         = data.Bio;
                _profile.AvatarUrl   = data.AvatarUrl;
                _profile.CreatedAt   = data.CreatedAt;
            }

            // Pull email-verified flag from Firebase auth session
            _profile.IsEmailVerified = AuthService.IsEmailVerified;

            // Bind fields
            txtUsername.Text    = string.IsNullOrEmpty(_profile.Username) ? "(chưa đặt)" : _profile.Username;
            txtDisplayName.Text = _profile.DisplayName;
            txtBio.Text         = _profile.Bio;
            txtCreatedAt.Text   = _profile.CreatedAtText;
            txtHeaderName.Text  = string.IsNullOrEmpty(_profile.DisplayName) ? "(Chưa đặt tên)" : _profile.DisplayName;
            txtHeaderEmail.Text = _profile.Email;
            txtInitials.Text    = GetInitials(_profile.DisplayName);

            // Email verification badge coloring
            txtEmailVerified.Text = _profile.EmailVerifiedText;
            badgeVerified.Background = _profile.IsEmailVerified
                ? new System.Windows.Media.LinearGradientBrush(
                      System.Windows.Media.Color.FromRgb(236, 253, 245),
                      System.Windows.Media.Color.FromRgb(209, 250, 229), 0)
                : new System.Windows.Media.LinearGradientBrush(
                      System.Windows.Media.Color.FromRgb(255, 251, 235),
                      System.Windows.Media.Color.FromRgb(254, 243, 199), 0);
            txtEmailVerified.Foreground = _profile.IsEmailVerified
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6));

            // Load avatar image
            LoadAvatarImage(_profile.AvatarUrl);
        }

        // ── AVATAR: pick file ─────────────────────────────────────────────────────
        private async void btnChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Chọn ảnh đại diện",
                Filter = "Hình ảnh|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };
            if (dlg.ShowDialog() != true) return;

            _localAvatarPath = dlg.FileName;

            // Preview immediately
            LoadAvatarImage(_localAvatarPath);
            _profile.HasAvatar.ToString(); // trigger binding refresh via AvatarUrl
            _profile.AvatarUrl = _localAvatarPath;

            // Copy to Assets/Avatars for local cache
            string destDir  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatars");
            Directory.CreateDirectory(destDir);
            string ext      = Path.GetExtension(_localAvatarPath);
            string destFile = Path.Combine(destDir, AuthService.CurrentUserId + ext);

            try { File.Copy(_localAvatarPath, destFile, overwrite: true); }
            catch { /* non-fatal: just use original path */ }

            // Attempt Firebase Storage upload (non-blocking, falls back to local path)
            string finalUrl = await UploadAvatarToFirebaseAsync(_localAvatarPath);
            if (string.IsNullOrEmpty(finalUrl)) finalUrl = destFile; // local fallback

            // Persist to DB
            bool saved = await _dbService.UpdateAvatarUrlAsync(AuthService.CurrentUserId!, finalUrl);
            if (saved)
            {
                _profile.AvatarUrl = finalUrl;
                MessageBox.Show("Ảnh đại diện đã được cập nhật!", "Thành công",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadAvatarImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource       = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                      ? new Uri(path)
                                      : new Uri(path, UriKind.Absolute);
                bmp.CacheOption     = BitmapCacheOption.OnLoad;
                bmp.CreateOptions   = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                imgAvatar.Source = bmp;
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadAvatarImage error: " + ex.Message);
            }
        }

        private async Task<string> UploadAvatarToFirebaseAsync(string localPath)
        {
            try
            {
                string bucket = Environment.GetEnvironmentVariable("FIREBASE_STORAGE_BUCKET") ?? string.Empty;
                if (string.IsNullOrEmpty(bucket)) return string.Empty;

                using var stream = File.OpenRead(localPath);
                string fileName  = AuthService.CurrentUserId + Path.GetExtension(localPath);
                string url = await new FirebaseStorage(
                    bucket,
                    new FirebaseStorageOptions { ThrowOnCancel = true })
                    .Child("avatars")
                    .Child(fileName)
                    .PutAsync(stream);

                return url;
            }
            catch (Exception ex)
            {
                Console.WriteLine("UploadAvatar Firebase Error: " + ex.Message);
                return string.Empty;
            }
        }

        // ── SAVE PROFILE ─────────────────────────────────────────────────────────
        private async void btnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string displayName = txtDisplayName.Text.Trim();
            string bio         = txtBio.Text.Trim();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                MessageBox.Show("Tên hiển thị không được để trống.", "Lỗi nhập liệu",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (displayName.Length > 50)
            {
                MessageBox.Show("Tên hiển thị tối đa 50 ký tự.", "Lỗi nhập liệu",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnSaveProfile.IsEnabled = false;
            btnSaveProfile.Content   = "⏳ Đang lưu...";

            bool ok = await _dbService.UpdateProfileAsync(
                AuthService.CurrentUserId!, displayName, bio);

            btnSaveProfile.IsEnabled = true;
            btnSaveProfile.Content   = "💾  Lưu thay đổi";

            if (ok)
            {
                _profile.DisplayName = displayName;
                _profile.Bio         = bio;
                txtHeaderName.Text   = displayName;
                txtInitials.Text     = GetInitials(displayName);
                MessageBox.Show("Cập nhật thành công!", "Thành công",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Cập nhật thất bại. Vui lòng thử lại.", "Lỗi",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── CHANGE PASSWORD ──────────────────────────────────────────────────────
        private async void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string email = _profile.Email;
            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Không tìm thấy địa chỉ email.", "Lỗi",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"Gửi email đặt lại mật khẩu tới:\n{email}?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                btnChangePassword.IsEnabled = false;
                await AuthService.SendPasswordResetEmailAsync(email);
                MessageBox.Show("Email đặt lại mật khẩu đã được gửi.\nVui lòng kiểm tra hộp thư.",
                                "Đã gửi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message, "Lỗi",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnChangePassword.IsEnabled = true;
            }
        }

        // ── LOGOUT ───────────────────────────────────────────────────────────────
        private async void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận",
                                      MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            // Mark offline before logout
            if (!string.IsNullOrEmpty(AuthService.CurrentUserId))
                await _dbService.SetOnlineStatusAsync(AuthService.CurrentUserId, false);

            AuthService.Logout();

            var login = new MainWindow();
            login.Show();

            var toClose = Application.Current.Windows.OfType<Window>().ToList();
            foreach (var w in toClose)
                if (w != login) w.Close();
        }

        // ── HELPERS ──────────────────────────────────────────────────────────────
        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : name[0].ToString().ToUpper();
        }
    }
}