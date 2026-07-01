// Standardized to production level
// Purpose: Create a new group conversation with search suggestion and chips
// Dependencies: ApiClient, AuthService
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hermes
{
    public partial class CreateChatWindow : Window
    {
        public bool IsGroup { get; } = true;
        public string ChatName { get; private set; } = "";
        public string[] Participants { get; private set; } = Array.Empty<string>();
        public string[] UserIds { get; private set; } = Array.Empty<string>();

        private ObservableCollection<SearchResultItem> _selectedMembers = new ObservableCollection<SearchResultItem>();
        private ObservableCollection<SearchResultItem> _suggestions = new ObservableCollection<SearchResultItem>();
        private CancellationTokenSource? _searchCts;

        public CreateChatWindow()
        {
            InitializeComponent();
            icSelectedMembers.ItemsSource = _selectedMembers;
            icSuggestions.ItemsSource = _suggestions;
        }

        private async void txtSearchMember_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyword = txtSearchMember.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(keyword))
            {
                _searchCts?.Cancel();
                _suggestions.Clear();
                bdSuggestions.Visibility = Visibility.Collapsed;
                return;
            }

            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var users = await Backend.Services.ApiClient.SearchUsersAsync(keyword);
                if (token.IsCancellationRequested) return;

                Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(txtSearchMember.Text))
                    {
                        _suggestions.Clear();
                        bdSuggestions.Visibility = Visibility.Collapsed;
                        return;
                    }

                    _suggestions.Clear();
                    if (users != null && users.Any())
                    {
                        string[] palette = { "#3B82F6", "#8B5CF6", "#EC4899", "#10B981", "#F59E0B", "#EF4444" };
                        foreach (var u in users)
                        {
                            if (u.UserId == AuthService.CurrentUserId) continue; // Ẩn bản thân
                            if (_selectedMembers.Any(sm => sm.UserId == u.UserId)) continue; // Ẩn người đã chọn

                            string displayName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserId ?? "Người dùng";
                            string initials = displayName.Length > 0 ? displayName.Substring(0, 1).ToUpper() : "?";
                            string avatarColor = palette[Math.Abs(displayName.GetHashCode()) % palette.Length];

                            _suggestions.Add(new SearchResultItem
                            {
                                UserId = u.UserId ?? "",
                                DisplayName = displayName,
                                Identifier = u.UserId ?? keyword,
                                Initials = initials,
                                AvatarColor = avatarColor
                            });
                        }

                        bdSuggestions.Visibility = _suggestions.Any() ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        bdSuggestions.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch { bdSuggestions.Visibility = Visibility.Collapsed; }
        }

        private void SuggestionItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SearchResultItem item)
            {
                if (!_selectedMembers.Any(sm => sm.UserId == item.UserId))
                {
                    _selectedMembers.Add(item);
                }
                txtSearchMember.Text = "";
                _suggestions.Clear();
                bdSuggestions.Visibility = Visibility.Collapsed;
                txtSearchMember.Focus();
            }
        }

        private void RemoveMember_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string userId)
            {
                var target = _selectedMembers.FirstOrDefault(sm => sm.UserId == userId);
                if (target != null)
                {
                    _selectedMembers.Remove(target);
                }
            }
        }

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            string groupName = txtGroupName.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(groupName))
            {
                MessageBox.Show("Vui lòng nhập tên nhóm!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGroupName.Focus();
                return;
            }

            if (_selectedMembers.Count < 2)
            {
                MessageBox.Show("Nhóm phải có tối thiểu 2 người tham gia khác bạn.", "Thiếu thành viên", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var participantsList = new List<string>();
            var userIdsList = new List<string>();

            foreach (var sm in _selectedMembers)
            {
                if (!userIdsList.Contains(sm.UserId))
                {
                    userIdsList.Add(sm.UserId);
                    participantsList.Add(sm.DisplayName);
                }
            }

            string myId = AuthService.CurrentUserId;
            string myFullName = AuthService.CurrentFullName;
            if (!string.IsNullOrEmpty(myId) && !userIdsList.Contains(myId))
            {
                userIdsList.Add(myId);
                if (!string.IsNullOrEmpty(myFullName))
                    participantsList.Add(myFullName);
            }

            ChatName = groupName;
            Participants = participantsList.ToArray();
            UserIds = userIdsList.ToArray();

            this.DialogResult = true;
            this.Close();
        }
    }
}