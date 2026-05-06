using System;
using System.Linq;
using System.Windows;

namespace Hermes
{
    public partial class CreateChatWindow : Window
    {
        public bool IsGroup { get; private set; }
        public string ChatName { get; private set; }
        public string[] Participants { get; private set; }
        public string[] UserIds { get; private set; }

        public CreateChatWindow()
        {
            InitializeComponent();
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

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetInput = txtTarget.Text.Trim();
                if (string.IsNullOrEmpty(targetInput))
                {
                    MessageBox.Show("Vui lòng nhập đối tượng nhắn tin!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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
                                             .ToList();

                    if (targets.Count < 2)
                    {
                        MessageBox.Show("Nhóm phải có tối thiểu 2 người tham gia khác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var invalidUsers = targets.Where(t => Hermes.Backend.Services.ConversationService.GetUserByIdentifier(t).UserId == null).ToList();
                    if (invalidUsers.Any())
                    {
                        MessageBox.Show("Không tìm thấy các tài khoản sau:\n" + string.Join("\n", invalidUsers), "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    IsGroup = true;
                    ChatName = groupName;

                    var participantsList = targets.Select(t => Hermes.Backend.Services.ConversationService.GetUserByIdentifier(t).FullName).ToList();
                    var userIdsList = targets.Select(t => Hermes.Backend.Services.ConversationService.GetUserByIdentifier(t).UserId).ToList();

                    // Optional: Get current user's full name to append to Participants
                    string currentFullName = AuthService.GetUsernameByIdentifier(AuthService.CurrentUserId);
                    if (!string.IsNullOrEmpty(currentFullName)) participantsList.Add(currentFullName);
                    userIdsList.Add(AuthService.CurrentUserId);

                    Participants = participantsList.ToArray();
                    UserIds = userIdsList.ToArray();
                }
                else
                {
                    var user = Hermes.Backend.Services.ConversationService.GetUserByIdentifier(targetInput);
                    if (user.UserId == null)
                    {
                        MessageBox.Show("Không tìm thấy tài khoản này!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    IsGroup = false;
                    ChatName = user.FullName;

                    var participantsList = new System.Collections.Generic.List<string> { user.FullName };
                    string currentFullName = AuthService.GetUsernameByIdentifier(AuthService.CurrentUserId);
                    if (!string.IsNullOrEmpty(currentFullName)) participantsList.Add(currentFullName);

                    Participants = participantsList.ToArray();
                    UserIds = new[] { user.UserId, AuthService.CurrentUserId };
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