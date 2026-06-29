// Standardized to production level
// Purpose: Create a new group conversation
// Dependencies: ApiClient, AuthService
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Hermes
{
    public partial class CreateChatWindow : Window
    {
        // IsGroup luôn true vì cửa sổ này chỉ dành cho nhóm
        public bool IsGroup { get; } = true;
        public string ChatName { get; private set; }
        public string[] Participants { get; private set; }
        public string[] UserIds { get; private set; }

        public CreateChatWindow()
        {
            InitializeComponent();
        }

        private async void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            // NULL GUARD
            if (txtGroupName == null || txtTarget == null) return;

            try
            {
                string groupName = txtGroupName.Text?.Trim() ?? string.Empty;
                string targetInput = txtTarget.Text?.Trim() ?? string.Empty;

                // Kiểm tra tên nhóm
                if (string.IsNullOrEmpty(groupName))
                {
                    MessageBox.Show("Vui lòng nhập tên nhóm!", "Thiếu thông tin",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtGroupName.Focus();
                    return;
                }

                // Kiểm tra danh sách thành viên
                if (string.IsNullOrEmpty(targetInput))
                {
                    MessageBox.Show("Vui lòng nhập danh sách thành viên!", "Thiếu thông tin",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtTarget.Focus();
                    return;
                }

                var targets = targetInput
                    .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList();

                if (targets.Count < 2)
                {
                    MessageBox.Show("Nhóm phải có tối thiểu 2 người tham gia khác bạn.",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Disable nút để tránh double-click
                var btn = sender as System.Windows.Controls.Button;
                if (btn != null) btn.IsEnabled = false;

                var participantsList = new List<string>();
                var userIdsList      = new List<string>();

                foreach (var target in targets)
                {
                    var user = await Hermes.Backend.Services.ApiClient.GetUserByIdentifierAsync(target);
                    if (user == null)
                    {
                        MessageBox.Show($"Không tìm thấy tài khoản: «{target}»\nVui lòng kiểm tra lại username hoặc email.",
                            "Không tìm thấy", MessageBoxButton.OK, MessageBoxImage.Error);
                        if (btn != null) btn.IsEnabled = true;
                        return;
                    }

                    // Tránh trùng userId
                    if (!string.IsNullOrEmpty(user.UserId) && !userIdsList.Contains(user.UserId))
                    {
                        participantsList.Add(user.FullName ?? target);
                        userIdsList.Add(user.UserId);
                    }
                }

                // Thêm bản thân vào nhóm
                string myId       = AuthService.CurrentUserId;
                string myFullName = AuthService.CurrentFullName;
                if (!string.IsNullOrEmpty(myId) && !userIdsList.Contains(myId))
                {
                    userIdsList.Add(myId);
                    if (!string.IsNullOrEmpty(myFullName))
                        participantsList.Add(myFullName);
                }

                ChatName     = groupName;
                Participants = participantsList.ToArray();
                UserIds      = userIdsList.ToArray();

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}