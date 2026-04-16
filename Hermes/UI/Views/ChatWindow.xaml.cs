using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hermes.Models;

namespace Hermes
{
    public partial class ChatWindow : Window
    {
        public ObservableCollection<ChatModel> Chats { get; set; }

        public ChatWindow()
        {
            InitializeComponent();
            Chats = new ObservableCollection<ChatModel>();

            // Fake data init
            LoadMockData();

            lstChats.ItemsSource = Chats;
            if (Chats.Any())
            {
                lstChats.SelectedIndex = 0;
            }
        }

        private void LoadMockData()
        {
            var chat1 = new ChatModel { ChatId = "1", ChatName = "Hoàng Hải", Initials = "H", AvatarColor = "#9CA3AF", LastMessage = "You: Nghe hợp lý đấy! Mấy giờ đi...", LastMessageTime = "10:35 AM" };
            chat1.Messages.Add(new MessageModel { SenderName = "Hoàng Hải", Content = "Cuối tuần này cậu có rảnh không? Bọn mình đi cà phê rồi xem phim luôn nhé?", Time = "10:31 AM", IsMine = false });
            chat1.Messages.Add(new MessageModel { SenderName = "You", Content = "Nghe hợp lý đấy! Mấy giờ đi được để mình đặt vé trước?", Time = "10:35 AM", IsMine = true });

            var chat2 = new ChatModel { ChatId = "2", ChatName = "Team Phượt Xuyên Việt", Initials = "T", AvatarColor = "#10B981", LastMessage = "Tuấn: Cuối tuần nhớ mang theo áo...", LastMessageTime = "09:15 AM" };
            chat2.Messages.Add(new MessageModel { SenderName = "Tuấn", Content = "Cuối tuần nhớ mang theo áo khoác nhé mọi người!", Time = "09:10 AM", IsMine = false });

            Chats.Add(chat1);
            Chats.Add(chat2);
        }

        private void lstChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstChats.SelectedItem is ChatModel selectedChat)
            {
                txtCurrentChatName.Text = selectedChat.ChatName;
                icMessages.ItemsSource = selectedChat.Messages;
            }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow sw = new SettingsWindow();
            sw.Owner = this;
            sw.ShowDialog();
        }

        private void btnAddChat_Click(object sender, RoutedEventArgs e)
        {
            CreateChatWindow createChat = new CreateChatWindow();
            createChat.Owner = this;
            if (createChat.ShowDialog() == true)
            {
                string newChatName = createChat.ChatName;
                var newChat = new ChatModel 
                { 
                    ChatId = Guid.NewGuid().ToString(), 
                    ChatName = newChatName, 
                    Initials = createChat.IsGroup ? "G" : (newChatName.Length > 0 ? newChatName.Substring(0, 1).ToUpper() : ""), 
                    AvatarColor = createChat.IsGroup ? "#10B981" : "#F59E0B", 
                    LastMessage = "Bắt đầu cuộc trò chuyện...", 
                    LastMessageTime = DateTime.Now.ToString("hh:mm tt") 
                };
                Chats.Insert(0, newChat);
                lstChats.SelectedItem = newChat;
            }
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMessageInput.Text)) return;
            if (lstChats.SelectedItem is ChatModel currentChat)
            {
                currentChat.Messages.Add(new MessageModel { SenderName = "You", Content = txtMessageInput.Text.Trim(), Time = DateTime.Now.ToString("hh:mm tt"), IsMine = true });
                currentChat.LastMessage = "You: " + txtMessageInput.Text.Trim();
                currentChat.LastMessageTime = DateTime.Now.ToString("hh:mm tt");
                txtMessageInput.Text = "";

                // Scroll to bottom logically -> visual would need ScrollViewer
                svMessages.ScrollToEnd();
            }
        }
    }
}
