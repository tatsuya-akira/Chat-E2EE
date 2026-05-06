using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Hermes.Services;

namespace Hermes
{
    public partial class CreateChatWindow : Window
    {
        public bool IsGroup { get; private set; }
        public string ChatName { get; private set; }
        public string[] Participants { get; private set; }

        private string _currentUserId;

        public CreateChatWindow(string userId)
        {
            InitializeComponent();
            _currentUserId = userId;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (spGroupName == null || lblTarget == null || txtTarget == null) return;

            if (rbGroup.IsChecked == true)
            {
                spGroupName.Visibility = Visibility.Visible;
                lblTarget.Text = "Nhập các Username/Email (ngăn cách bởi dấu phẩy):";
                txtTarget.ToolTip = "Ví dụ: user1, user2@gmail.com";
            }
            else
            {
                spGroupName.Visibility = Visibility.Collapsed;
                lblTarget.Text = "Nhập Username hoặc Email:";
                txtTarget.ToolTip = "Ví dụ: user1";
            }
        }

        private async void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetInput = txtTarget.Text.Trim();
                if (string.IsNullOrEmpty(targetInput))
                {
                    MessageBox.Show("Vui lòng nhập đối tượng nhắn tin!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserId))
                {
                    MessageBox.Show("Lỗi: Không xác định được danh tính người dùng. Vui lòng đăng xuất và đăng nhập lại!", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dbService = new DatabaseService();

                if (rbGroup.IsChecked == true)
                {
                    string groupName = txtGroupName.Text.Trim();
                    if (string.IsNullOrEmpty(groupName))
                    {
                        MessageBox.Show("Vui lòng nhập tên nhóm!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var targets = targetInput.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(t => t.Trim())
                                             .Distinct()
                                             .ToArray();

                    if (targets.Length < 2)
                    {
                        MessageBox.Show("Nhóm phải có tối thiểu 2 người tham gia khác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    List<string> validParticipantIds = new List<string>();
                    List<string> invalidUsers = new List<string>();

                    foreach (var t in targets)
                    {
                        string userId = AuthService.GetUserIdByIdentifier(t);
                        
                        if (string.IsNullOrEmpty(userId))
                        {
                            invalidUsers.Add(t);
                        }
                        else if (userId == _currentUserId)
                        {
                            MessageBox.Show("Bạn không thể tự thêm chính mình vào nhóm qua ô này!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else
                        {
                            validParticipantIds.Add(userId);
                        }
                    }

                    if (invalidUsers.Any())
                    {
                        MessageBox.Show("Không tìm thấy các tài khoản sau:\n" + string.Join("\n", invalidUsers), "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    IsGroup = true;
                    ChatName = groupName;
                    Participants = validParticipantIds.ToArray();

                    await dbService.CreateConversationAsync(_currentUserId, validParticipantIds, true, groupName);
                }
                else
                {
                    string userId = AuthService.GetUserIdByIdentifier(targetInput);

                    if (string.IsNullOrEmpty(userId))
                    {
                        MessageBox.Show("Không tìm thấy tài khoản này trên hệ thống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (userId == _currentUserId)
                    {
                        MessageBox.Show("Bạn không thể tự tạo cuộc trò chuyện với chính mình!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    IsGroup = false;
                    ChatName = AuthService.GetUsernameByIdentifier(targetInput);
                    Participants = new[] { userId };

                    await dbService.CreateConversationAsync(_currentUserId, new List<string> { userId }, false);
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối hoặc hệ thống: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}