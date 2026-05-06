// Standardized to production level
// Purpose: ChatWindow code-behind — all features wired: media, emoji, call signals, seen-status
// Dependencies: DatabaseService, AuthService, Firebase.Database, FirebaseStorage.net

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using Hermes.Models;
using Hermes.Services;
using Microsoft.Win32;

namespace Hermes
{
    public partial class ChatWindow : Window
    {
        private readonly DatabaseService _dbService = new DatabaseService();
        private string _currentUserId;

        public ObservableCollection<ChatModel> Chats { get; set; }

        private Firebase.Database.FirebaseClient _firebaseClient;
        private IDisposable _userSyncSubscription;
        private IDisposable _messagesSubscription;
        private IDisposable _seenSyncSubscription;

        public ChatWindow(string userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            Chats = new ObservableCollection<ChatModel>();

            LoadRealData();
            SetupFirebaseListener();

            lstChats.ItemsSource = Chats;
        }

        // ──────────────────────────────────────────────────────────────────────
        // FIREBASE SETUP
        // ──────────────────────────────────────────────────────────────────────
        private void SetupFirebaseListener()
        {
            if (string.IsNullOrEmpty(_currentUserId)) return;

            string firebaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") ?? "https://hermes-default-rtdb.firebaseio.com/";
            string firebaseSecret = Environment.GetEnvironmentVariable("FIREBASE_SECRET");

            _firebaseClient = new Firebase.Database.FirebaseClient(firebaseUrl, new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(firebaseSecret)
            });

            try
            {
                _userSyncSubscription = _firebaseClient
                    .Child("user_sync")
                    .Child(_currentUserId)
                    .AsObservable<object>()
                    .Subscribe(
                        d =>
                        {
                            if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                                Dispatcher.Invoke(LoadRealData);
                        },
                        ex => Console.WriteLine("Firebase user_sync error: " + ex.Message)
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firebase Setup Error: " + ex.Message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _userSyncSubscription?.Dispose();
            _messagesSubscription?.Dispose();
            _seenSyncSubscription?.Dispose();
            base.OnClosed(e);
        }

        // ──────────────────────────────────────────────────────────────────────
        // DATA LOADING
        // ──────────────────────────────────────────────────────────────────────
        private void LoadRealData()
        {
            if (string.IsNullOrEmpty(_currentUserId)) return;
            var realChats = _dbService.GetUserChats(_currentUserId);
            Chats.Clear();
            foreach (var chat in realChats)
                Chats.Add(chat);
        }

        // ──────────────────────────────────────────────────────────────────────
        // SEARCH
        // ──────────────────────────────────────────────────────────────────────
        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text == "Tìm kiếm người thân, bạn bè...")
            {
                txtSearch.Text = "";
                txtSearch.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55));
            }
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
                txtSearch.Text = "Tìm kiếm người thân, bạn bè...";
                lstChats.ItemsSource = Chats;
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = txtSearch.Text.Trim();
            if (query == "Tìm kiếm người thân, bạn bè...") return;

            lstChats.ItemsSource = string.IsNullOrEmpty(query)
                ? (System.Collections.IEnumerable)Chats
                : Chats.Where(c => c.ChatName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // CHAT SELECTION  (load messages + mark seen + wire realtime listeners)
        // ──────────────────────────────────────────────────────────────────────
        private void lstChats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstChats.SelectedItem is ChatModel selectedChat)
            {
                EmptyStateArea.Visibility = Visibility.Collapsed;
                ChatArea.Visibility       = Visibility.Visible;
                txtCurrentChatName.Text   = selectedChat.ChatName;

                long convId = long.Parse(selectedChat.ChatId);

                // Load message history from MySQL
                var msgs = _dbService.GetMessages(convId, _currentUserId);
                selectedChat.Messages.Clear();
                foreach (var msg in msgs)
                    selectedChat.Messages.Add(msg);

                icMessages.ItemsSource = selectedChat.Messages;
                svMessages.ScrollToEnd();

                // Mark all messages in this conversation as seen
                _ = _dbService.MarkSeenAsync(convId, _currentUserId);

                // Dispose previous subscriptions
                _messagesSubscription?.Dispose();
                _seenSyncSubscription?.Dispose();

                if (_firebaseClient == null) return;

                // Realtime: new incoming messages
                try
                {
                    _messagesSubscription = _firebaseClient
                        .Child("conversations")
                        .Child(selectedChat.ChatId)
                        .Child("messages")
                        .AsObservable<dynamic>()
                        .Subscribe(
                            d =>
                            {
                                if (d.EventType != Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate) return;
                                Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        string senderId = (string)d.Object.senderId;
                                        string content  = (string)d.Object.content;
                                        if (senderId == _currentUserId) return;

                                        string senderName = AuthService.GetUsernameByIdentifier(senderId) ?? senderId;
                                        selectedChat.Messages.Add(new MessageModel
                                        {
                                            SenderName = senderName,
                                            Content    = content,
                                            Time       = DateTime.Now.ToString("HH:mm"),
                                            IsMine     = false
                                        });
                                        svMessages.ScrollToEnd();
                                        selectedChat.LastMessage     = senderName + ": " + content;
                                        selectedChat.LastMessageTime = DateTime.Now.ToString("HH:mm");

                                        // Auto-mark seen
                                        _ = _dbService.MarkSeenAsync(convId, _currentUserId);
                                    }
                                    catch { }
                                });
                            },
                            ex => Console.WriteLine("Firebase messages error: " + ex.Message)
                        );
                }
                catch (Exception ex) { Console.WriteLine("Firebase messages setup error: " + ex.Message); }

                // Realtime: seen-sync (update blue ticks on sender's screen)
                try
                {
                    _seenSyncSubscription = _firebaseClient
                        .Child("seen_sync")
                        .Child(selectedChat.ChatId)
                        .AsObservable<dynamic>()
                        .Subscribe(
                            d =>
                            {
                                if (d.EventType != Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate) return;
                                Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        if (d.Key == _currentUserId) return;
                                        long peerLastSeen = (long)d.Object.lastSeenMessageId;
                                        foreach (var msg in selectedChat.Messages)
                                            if (msg.IsMine && msg.MessageId <= peerLastSeen && !msg.IsSeen)
                                                msg.IsSeen = true;
                                    }
                                    catch { }
                                });
                            },
                            ex => Console.WriteLine("Firebase seen_sync error: " + ex.Message)
                        );
                }
                catch (Exception ex) { Console.WriteLine("Firebase seen_sync setup error: " + ex.Message); }
            }
            else
            {
                EmptyStateArea.Visibility = Visibility.Visible;
                ChatArea.Visibility       = Visibility.Collapsed;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // SEND MESSAGE
        // ──────────────────────────────────────────────────────────────────────
        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentMessage();
        }

        private async void txtMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter without Shift → send
            if (e.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                await SendCurrentMessage();
            }
        }

        private async Task SendCurrentMessage()
        {
            string text = txtMessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (lstChats.SelectedItem is not ChatModel currentChat) return;

            txtMessageInput.Text = "";

            var newMsg = new MessageModel
            {
                SenderName = "You",
                Content    = text,
                Time       = DateTime.Now.ToString("HH:mm"),
                IsMine     = true,
                IsSeen     = false
            };
            currentChat.Messages.Add(newMsg);
            currentChat.LastMessage     = "You: " + text;
            currentChat.LastMessageTime = DateTime.Now.ToString("HH:mm");
            svMessages.ScrollToEnd();

            long convId = long.Parse(currentChat.ChatId);
            long msgId  = await _dbService.SendMessageAsync(convId, _currentUserId, text, new System.Collections.Generic.List<string> { _currentUserId });
            newMsg.MessageId = msgId; // assign real DB ID for seen tracking
        }

        // ──────────────────────────────────────────────────────────────────────
        // ATTACH FILE / IMAGE
        // ──────────────────────────────────────────────────────────────────────
        private async void btnAttach_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is not ChatModel currentChat)
            {
                MessageBox.Show("Vui lòng chọn cuộc trò chuyện trước!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title  = "Chọn file hoặc ảnh để gửi",
                Filter = "Tất cả file|*.*|Hình ảnh|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|Tài liệu|*.pdf;*.doc;*.docx;*.xlsx;*.txt"
            };
            if (dlg.ShowDialog() != true) return;

            string filePath = dlg.FileName;
            string fileName = Path.GetFileName(filePath);

            try
            {
                // Show uploading placeholder
                var uploadingMsg = new MessageModel
                {
                    SenderName = "You",
                    Content    = $"⏳ Đang tải lên: {fileName}",
                    Time       = DateTime.Now.ToString("HH:mm"),
                    IsMine     = true
                };
                currentChat.Messages.Add(uploadingMsg);
                svMessages.ScrollToEnd();

                string storageBucket = Environment.GetEnvironmentVariable("FIREBASE_STORAGE_BUCKET") ?? "hermes-chat-uit.appspot.com";
                string downloadUrl;

                using (var fileStream = File.OpenRead(filePath))
                {
                    var task = new FirebaseStorage(
                        storageBucket,
                        new FirebaseStorageOptions { ThrowOnCancel = true }
                    )
                    .Child("uploads")
                    .Child(_currentUserId)
                    .Child(DateTime.UtcNow.Ticks + "_" + fileName)
                    .PutAsync(fileStream);

                    downloadUrl = await task;
                }

                // Replace placeholder with actual URL
                currentChat.Messages.Remove(uploadingMsg);

                var fileMsg = new MessageModel
                {
                    SenderName = "You",
                    Content    = downloadUrl,
                    Time       = DateTime.Now.ToString("HH:mm"),
                    IsMine     = true,
                    IsSeen     = false
                };
                currentChat.Messages.Add(fileMsg);
                currentChat.LastMessage     = "You: [Tệp đính kèm]";
                currentChat.LastMessageTime = DateTime.Now.ToString("HH:mm");
                svMessages.ScrollToEnd();

                long convId = long.Parse(currentChat.ChatId);
                long msgId  = await _dbService.SendMessageAsync(convId, _currentUserId, downloadUrl, new System.Collections.Generic.List<string> { _currentUserId });
                fileMsg.MessageId = msgId;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // EMOJI PICKER
        // ──────────────────────────────────────────────────────────────────────
        private void btnEmoji_Click(object sender, RoutedEventArgs e)
        {
            emojiPopup.IsOpen = !emojiPopup.IsOpen;
        }

        private void btnEmojiPick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                txtMessageInput.Text += btn.Content?.ToString();
                txtMessageInput.CaretIndex = txtMessageInput.Text.Length;
                emojiPopup.IsOpen = false;
                txtMessageInput.Focus();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // CALL FEATURES
        // ──────────────────────────────────────────────────────────────────────
        private async void btnVoiceCall_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is not ChatModel chat) return;
            await PushCallSignalAsync(chat, "voice");
            new CallWindow(_currentUserId, chat.ChatName, "voice").Show();
        }

        private async void btnVideoCall_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is not ChatModel chat) return;
            await PushCallSignalAsync(chat, "video");
            new CallWindow(_currentUserId, chat.ChatName, "video").Show();
        }

        private async Task PushCallSignalAsync(ChatModel chat, string callType)
        {
            try
            {
                if (_firebaseClient == null) return;
                await _firebaseClient
                    .Child("calls")
                    .Child(chat.ChatId)
                    .PutAsync(new
                    {
                        callerId   = _currentUserId,
                        callerName = AuthService.GetUsernameByIdentifier(_currentUserId) ?? "Người dùng",
                        type       = callType,
                        status     = "calling",
                        at         = DateTime.UtcNow.ToString("o")
                    });
            }
            catch (Exception ex) { Console.WriteLine("Call signal error: " + ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────────────
        // 3-DOT MENU
        // ──────────────────────────────────────────────────────────────────────
        private void btnMoreOptions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void menuGroupInfo_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is ChatModel chat)
                MessageBox.Show($"Tên: {chat.ChatName}\nID: {chat.ChatId}", "Thông tin cuộc trò chuyện", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void menuMembers_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is not ChatModel chat) return;
            try
            {
                var members = _dbService.GetConversationMembers(long.Parse(chat.ChatId));
                string list = string.Join("\n", members.Select((m, i) => $"{i + 1}. {m}"));
                MessageBox.Show(string.IsNullOrEmpty(list) ? "Không có thành viên nào." : list, "Danh sách thành viên", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async void menuLeave_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is not ChatModel chat) return;
            var result = MessageBox.Show($"Bạn có chắc muốn rời khỏi \"{chat.ChatName}\"?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _dbService.LeaveConversationAsync(long.Parse(chat.ChatId), _currentUserId);
                LoadRealData();
                EmptyStateArea.Visibility = Visibility.Visible;
                ChatArea.Visibility       = Visibility.Collapsed;
                lstChats.SelectedItem     = null;
            }
            catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
        }

        // ──────────────────────────────────────────────────────────────────────
        // TOOLBAR BUTTONS
        // ──────────────────────────────────────────────────────────────────────
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow { Owner = this }.ShowDialog();
            LoadRealData();
        }

        private void btnAddChat_Click(object sender, RoutedEventArgs e)
        {
            var createChat = new CreateChatWindow(_currentUserId) { Owner = this };
            if (createChat.ShowDialog() == true)
                LoadRealData();
        }
    }
}