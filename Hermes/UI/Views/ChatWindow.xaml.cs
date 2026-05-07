using Hermes.Backend.Services;
using Hermes.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hermes
{
    public partial class ChatWindow : Window
    {
        public ObservableCollection<ChatModel> Chats { get; set; }

        public ChatWindow()
        {
            InitializeComponent();
            Chats = new ObservableCollection<ChatModel>();
            lstChats.ItemsSource = Chats;

            // Thay thế LoadMockData bằng dữ liệu thật
            LoadRealChatsAsync();
        }

        private async void LoadRealChatsAsync()
        {
            // Gọi API lấy danh sách phòng chat của mình
            var myChats = await ApiClient.GetMyChatsAsync(AuthService.CurrentUserId);

            Chats.Clear();
            foreach (var c in myChats)
            {
                string displayName = c.IsGroup ? c.GroupName : c.OtherUserName;

                Chats.Add(new ChatModel
                {
                    ChatId = c.ChatId,
                    ChatName = displayName,
                    Initials = c.IsGroup ? "G" : (string.IsNullOrEmpty(displayName) ? "" : displayName.Substring(0, 1).ToUpper()),
                    AvatarColor = c.IsGroup ? "#10B981" : "#F59E0B",
                    LastMessage = "Bấm để xem tin nhắn...",
                    LastMessageTime = ""
                });
            }

            // Tự động chọn phòng đầu tiên
            if (Chats.Any())
            {
                lstChats.SelectedIndex = 0;
            }
        }

        private async void lstChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstChats.SelectedItem is ChatModel selectedChat)
            {
                // 1. --- THÊM ĐOẠN NÀY ĐỂ CHUYỂN GIAO DIỆN ---
                // (Thay tên GridWelcome và GridChat bằng đúng x:Name trong file ChatWindow.xaml của bạn)
                if (GridWelcome != null) GridWelcome.Visibility = Visibility.Collapsed;
                if (GridChat != null) GridChat.Visibility = Visibility.Visible;

                // 2. Clear tin nhắn cũ trên UI
                selectedChat.Messages.Clear();

                // 3. Lấy lịch sử từ Database
                var history = await Backend.Services.ApiClient.GetChatHistoryAsync(int.Parse(selectedChat.ChatId));

                // 4. Đổ dữ liệu vào UI
                foreach (var msg in history)
                {
                    msg.IsMine = (msg.SenderId == AuthService.CurrentUserId);
                    selectedChat.Messages.Add(msg);
                }

                svMessages.ScrollToEnd();
            }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow sw = new SettingsWindow();
            sw.Owner = this;
            sw.ShowDialog();
        }

        // Đảm bảo hàm này nằm bên trong: public partial class ChatWindow : Window { ... }

        private async void btnAddChat_Click(object sender, RoutedEventArgs e)
        {
            CreateChatWindow createChat = new CreateChatWindow();
            createChat.Owner = this;
            if (createChat.ShowDialog() == true)
            {
                try
                {
                    string newChatName = createChat.ChatName;
                    var userIds = createChat.UserIds.ToList();

                    int newConvId = await Hermes.Backend.Services.ApiClient.CreateConversationAsync(createChat.IsGroup, createChat.IsGroup ? newChatName : null, userIds);

                    if (newConvId == -1)
                    {
                        MessageBox.Show("Lỗi Server khi tạo cuộc trò chuyện.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var newChat = new ChatModel
                    {
                        ChatId = newConvId.ToString(),
                        ChatName = newChatName,
                        Initials = createChat.IsGroup ? "G" : (newChatName.Length > 0 ? newChatName.Substring(0, 1).ToUpper() : ""),
                        AvatarColor = createChat.IsGroup ? "#10B981" : "#F59E0B",
                        LastMessage = "Bắt đầu cuộc trò chuyện...",
                        LastMessageTime = DateTime.Now.ToString("hh:mm tt")
                    };

                    Chats.Insert(0, newChat);
                    lstChats.SelectedItem = newChat;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi lưu cuộc trò chuyện: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMessageInput.Text)) return;

            if (lstChats.SelectedItem is ChatModel currentChat)
            {
                string plainText = txtMessageInput.Text.Trim();
                string currentTime = DateTime.Now.ToString("hh:mm tt");

                // 1. Cập nhật giao diện mượt mà (Giữ nguyên logic cũ)
                currentChat.Messages.Add(new MessageModel { SenderName = "You", Content = plainText, Time = currentTime, IsMine = true });
                currentChat.LastMessage = "You: " + plainText;
                currentChat.LastMessageTime = currentTime;
                txtMessageInput.Text = "";
                svMessages.ScrollToEnd();

                // --- 2. LOGIC BACKEND: LƯU DATABASE ---
                try
                {
                    // Parse ConversationId từ ChatId của UI
                    if (!int.TryParse(currentChat.ChatId, out int convId))
                    {
                        MessageBox.Show("Lỗi: ID cuộc trò chuyện không hợp lệ.");
                        return;
                    }

                    // Tạo DTO để gửi lên Server
                    var dto = new Hermes.Shared.DTOs.SendMessageDto
                    {
                        ConversationId = convId,
                        SenderId = AuthService.CurrentUserId,

                        CipherText = plainText,
                        TimeToLive = 0,

                        // TODO: Tạm thời để trống. Sau này sẽ mã hóa Session Key bằng RSA Public Key của đối phương
                        RecipientSessionKeys = new Dictionary<string, string>()
                    };

                    // Gọi API lưu tin nhắn xuống MySQL
                    bool isSaved = await Backend.Services.ApiClient.SaveMessageAsync(dto);

                    if (isSaved)
                    {
                        // --- 3. BẮN SIGNALR ĐỂ REAL-TIME ---
                        // Sẽ gọi _signalRService.SendMessageAsync(...) ở đây
                        Console.WriteLine("Đã lưu DB thành công. Chuẩn bị bắn SignalR...");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi hệ thống khi gửi tin nhắn: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}
