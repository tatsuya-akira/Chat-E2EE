using Hermes.Backend.Services;
using Hermes.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;

namespace Hermes
{
    public partial class ChatWindow : Window
    {
        public ObservableCollection<ChatModel> Chats { get; set; }
        private SignalRService _signalRService;

        public ChatWindow()
        {
            InitializeComponent();
            Chats = new ObservableCollection<ChatModel>();
            lstChats.ItemsSource = Chats;

            // 1. Khởi tạo SignalR
            _signalRService = new SignalRService("http://localhost:5042/chathub");

            // 2. Lắng nghe sự kiện nhắn tin tới
            _signalRService.OnReceiveMessage += SignalR_OnReceiveMessage;

            // 3. Tự động Connect
            this.Loaded += async (s, e) => {
                // BƯỚC A: Phải đợi kết nối SignalR thành công 100%...
                await _signalRService.ConnectAsync(AuthService.CurrentUserId);

                // BƯỚC B: ...Thì mới được gọi hàm lấy danh sách chat & chui vào phòng!
                LoadRealChatsAsync();
            };

            // (ĐÃ XÓA LoadRealChatsAsync() Ở ĐÂY)
        }

        // --- HÀM NÀY ĐÃ ĐƯỢC FIX ĐỂ NHẬN TIN NHẮN REAL-TIME CHUẨN XÁC ---
        private void SignalR_OnReceiveMessage(string conversationId, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Tìm đúng phòng chat đang nhận tin nhắn (Dựa vào ID thay vì SelectedItem)
                var targetChat = Chats.FirstOrDefault(c => c.ChatId == conversationId);

                if (targetChat != null)
                {
                    // Đẩy tin nhắn vào model
                    targetChat.Messages.Add(new MessageModel
                    {
                        SenderName = "Friend", // Tạm thời hiển thị chung
                        Content = message,
                        Time = DateTime.Now.ToString("hh:mm tt"),
                        IsMine = false
                    });

                    // Cập nhật dòng text nhỏ ở cột trái
                    targetChat.LastMessage = message;

                    // Nếu phòng đó ĐANG ĐƯỢC MỞ trên màn hình, thì cuộn xuống
                    if (lstChats.SelectedItem is ChatModel currentChat && currentChat.ChatId == conversationId)
                    {
                        svMessages.ScrollToEnd();
                    }
                }
            });
        }

        private async void LoadRealChatsAsync()
        {
            var myChats = await Backend.Services.ApiClient.GetMyChatsAsync(AuthService.CurrentUserId);
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

                // THÊM ĐÚNG DÒNG NÀY: Ép SignalR chui vào phòng này để chờ tin nhắn
                await _signalRService.JoinRoomAsync(c.ChatId);
            }

            if (Chats.Any()) lstChats.SelectedIndex = 0;
        }

        private async void lstChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstChats.SelectedItem is ChatModel selectedChat)
            {
                if (GridWelcome != null) GridWelcome.Visibility = Visibility.Collapsed;
                if (GridChat != null) GridChat.Visibility = Visibility.Visible;

                selectedChat.Messages.Clear();

                var history = await Backend.Services.ApiClient.GetChatHistoryAsync(int.Parse(selectedChat.ChatId));

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

                    int newConvId = await Backend.Services.ApiClient.CreateConversationAsync(createChat.IsGroup, createChat.IsGroup ? newChatName : null, userIds);

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

        // --- HÀM NÀY ĐÃ ĐƯỢC FIX ĐỂ PHÁT SIGNALR CHUẨN XÁC ---
        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMessageInput.Text)) return;

            if (lstChats.SelectedItem is ChatModel currentChat)
            {
                string plainText = txtMessageInput.Text.Trim();
                string currentTime = DateTime.Now.ToString("hh:mm tt");

                // 1. Cập nhật giao diện mượt mà
                currentChat.Messages.Add(new MessageModel { SenderName = "You", Content = plainText, Time = currentTime, IsMine = true });
                currentChat.LastMessage = "You: " + plainText;
                currentChat.LastMessageTime = currentTime;
                txtMessageInput.Text = "";
                svMessages.ScrollToEnd();

                // 2. LƯU DATABASE
                try
                {
                    if (!int.TryParse(currentChat.ChatId, out int convId)) return;

                    var dto = new Hermes.Shared.DTOs.SendMessageDto
                    {
                        ConversationId = convId,
                        SenderId = AuthService.CurrentUserId,
                        CipherText = plainText,
                        TimeToLive = 0,
                        RecipientSessionKeys = new Dictionary<string, string>()
                    };

                    bool isSaved = await Backend.Services.ApiClient.SaveMessageAsync(dto);

                    if (isSaved)
                    {
                        // 3. BẮN SIGNALR VÀO NHÓM (Dùng ChatId làm định danh)
                        // Bắn tin nhắn mang kèm ConversationId để máy kia biết nhét vào phòng nào
                        await _signalRService.SendMessageAsync(currentChat.ChatId, plainText);
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