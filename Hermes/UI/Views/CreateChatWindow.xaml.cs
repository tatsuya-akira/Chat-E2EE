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
                                             .ToArray();

                    if (targets.Length < 2)
                    {
                        MessageBox.Show("Nhóm phải có tối thiểu 2 người tham gia khác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var invalidUsers = targets.Where(t => string.IsNullOrEmpty(AuthService.GetUsernameByIdentifier(t))).ToList();
                    if (invalidUsers.Any())
                    {
                        MessageBox.Show("Không tìm thấy các tài khoản sau:\n" + string.Join("\n", invalidUsers), "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    IsGroup = true;
                    ChatName = groupName;
                    Participants = targets.Select(t => AuthService.GetUsernameByIdentifier(t)).ToArray();
                }
                else
                {
                    string username = AuthService.GetUsernameByIdentifier(targetInput);
                    if (string.IsNullOrEmpty(username))
                    {
                        MessageBox.Show("Không tìm thấy tài khoản này!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    IsGroup = false;
                    ChatName = username;
                    Participants = new[] { username };
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