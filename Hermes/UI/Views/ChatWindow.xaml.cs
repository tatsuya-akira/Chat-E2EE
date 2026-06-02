using Hermes.Backend.Services;
using Hermes.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using Hermes.Client.Services;

namespace Hermes
{
    public partial class ChatWindow : Window
    {
        public ObservableCollection<ChatModel> Chats { get; set; }
        private SignalRService _signalRService;
        private WebRTCService _webRTCService;
        private string _currentCallTargetId; // Lưu ID người mình đang gọi
        private string _incomingOffer;       // Lưu Lời mời khi người khác gọi tới
        private bool _isInCall = false;
        private bool _isRinging = false;
        public ChatWindow()
        {
            InitializeComponent();
            Chats = new ObservableCollection<ChatModel>();
            lstChats.ItemsSource = Chats;

            // 1. Khởi tạo SignalR
            _signalRService = new SignalRService("http://100.67.94.18:5042/chathub");

            // 2. Lắng nghe sự kiện nhắn tin tới
            _signalRService.OnReceiveMessage += SignalR_OnReceiveMessage;

            // 3. Tự động Connect
            this.Loaded += async (s, e) => {
                // BƯỚC A: Phải đợi kết nối SignalR thành công 100%...
                await _signalRService.ConnectAsync(AuthService.CurrentUserId);

                // BƯỚC B: ...Thì mới được gọi hàm lấy danh sách chat & chui vào phòng!
                LoadRealChatsAsync();
            };

            // Đăng ký nhận thông báo có phòng chat mới
            _signalRService.OnNewChatNotification += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    // GỌI LẠI HÀM TẢI DANH SÁCH CHAT
                    // Bỏ chữ 'await' đi vì hàm này là void
                    LoadRealChatsAsync();
                });
            };
            _webRTCService = new Hermes.Client.Services.WebRTCService();

            // Khi WebRTC tạo xong tín hiệu, đẩy nó qua SignalR
            _webRTCService.OnOfferReady += async (offer) => await _signalRService.SendWebRTCOfferAsync(_currentCallTargetId, offer);
            _webRTCService.OnAnswerReady += async (answer) => await _signalRService.SendWebRTCAnswerAsync(_currentCallTargetId, answer);
            _webRTCService.OnIceCandidateReady += async (candidate) => await _signalRService.SendIceCandidateAsync(_currentCallTargetId, candidate);

            // --- LẮNG NGHE TÍN HIỆU TỪ NGƯỜI KHÁC ---
            _signalRService.OnReceiveWebRTCOffer += (callerId, offer) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Chốt chặn: Đang gọi mà có người khác gọi tới thì bỏ qua
                    if (_isInCall || _isRinging) return;

                    _isRinging = true;
                    _currentCallTargetId = callerId; 
                    _incomingOffer = offer;          
                    
                    txtCallStatus.Text = "Ai đó đang gọi bạn...";
                    
                    // Reset lại giao diện chuẩn
                    btnAcceptCall.Visibility = Visibility.Visible; 
                    btnRejectCall.Content = "❌ Từ chối";
                    CallPopup.Visibility = Visibility.Visible; 
                });
            };

            _signalRService.OnReceiveWebRTCAnswer += (answer) =>
                Dispatcher.Invoke(() => _webRTCService.ReceiveAnswer(answer));

            _signalRService.OnReceiveIceCandidate += (candidate) =>
                Dispatcher.Invoke(() => _webRTCService.AddIceCandidate(candidate));

            // KHI ĐỐI PHƯƠNG CÚP MÁY HOẶC TỪ CHỐI
            _signalRService.OnCallEnded += () =>
            {
                Dispatcher.Invoke(async () =>
                {
                    await HandleEndCallLogic();
                    MessageBox.Show("Cuộc gọi đã kết thúc.");
                });
            };

            // KHI TRẠNG THÁI WEBRTC THAY ĐỔI
            _webRTCService.OnCallStateChanged += (state) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (state.ToLower() == "connected")
                    {
                        _isInCall = true; // Đánh dấu đã kết nối
                        _isRinging = false;
                        txtCallStatus.Text = "Đang trong cuộc gọi 📞";
                        btnAcceptCall.Visibility = Visibility.Collapsed; 
                        btnRejectCall.Content = "☎ Cúp máy";           
                    }
                    else if (state.ToLower() == "failed" || state.ToLower() == "disconnected" || state.ToLower() == "closed")
                    {
                        txtCallStatus.Text = "Đã kết thúc cuộc gọi.";
                        
                        // Đợi 1.5s rồi gọi người lao công ra dọn dẹp form
                        Task.Delay(1500).ContinueWith(async _ =>
                            await Dispatcher.Invoke(async () => await HandleEndCallLogic())
                        );
                    }
                });
            };
        }
        private async Task HandleEndCallLogic()
        {
            await _webRTCService.CloseCallAsync();

            _isInCall = false;
            _isRinging = false;

            // Đảm bảo đưa giao diện về trạng thái gốc chuẩn bị cho cuộc gọi sau
            Application.Current.Dispatcher.Invoke(() =>
            {
                CallPopup.Visibility = Visibility.Collapsed;
                btnAcceptCall.Visibility = Visibility.Visible; // QUAN TRỌNG: Hiện lại nút Nghe
                btnRejectCall.Content = "❌ Từ chối";          // QUAN TRỌNG: Trả lại text gốc
            });
        }
        // --- HÀM NÀY ĐÃ ĐƯỢC FIX ĐỂ NHẬN TIN NHẮN REAL-TIME CHUẨN XÁC ---
        private void SignalR_OnReceiveMessage(string conversationId, string cipherText, Dictionary<string, string> recipientKeys)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var targetChat = Chats.FirstOrDefault(c => c.ChatId == conversationId);
                if (targetChat != null)
                {
                    string plainText = "[Tin nhắn mã hóa]";

                    // --- BẮT ĐẦU GIẢI MÃ ---
                    // Tìm chìa khóa AES của riêng mình trong cái rổ chìa khóa Server gửi về
                    if (recipientKeys.TryGetValue(AuthService.CurrentUserId, out string myEncryptedSessionKey))
                    {
                        try
                        {
                            byte[] aesKey = Backend.Services.CryptoService.DecryptSessionKeyWithRSA(myEncryptedSessionKey, AuthService.CurrentPrivateKey);
                            plainText = Backend.Services.CryptoService.DecryptWithAES(cipherText, aesKey);
                        }
                        catch { plainText = "[Lỗi giải mã E2EE]"; }
                    }
                    // -----------------------

                    targetChat.Messages.Add(new MessageModel
                    {
                        SenderName = "Friend",
                        Content = plainText, // Gán text đã giải mã thành công vào UI
                        Time = DateTime.Now.ToString("hh:mm tt"),
                        IsMine = false
                    });

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

                // Thay dòng gọi API cũ bằng dòng này
                var history = await Backend.Services.ApiClient.GetChatHistoryAsync(int.Parse(selectedChat.ChatId), AuthService.CurrentUserId);

                foreach (var msg in history)
                {
                    msg.IsMine = (msg.SenderId == AuthService.CurrentUserId);

                    // --- BẮT ĐẦU GIẢI MÃ ---
                    // Đã xóa "!msg.IsMine &&", giờ tin nào có khóa là giải mã láng hết!
                    if (!string.IsNullOrEmpty(msg.EncryptedSessionKey))
                    {
                        try
                        {
                            // 1. Mở khóa AES bằng Private Key của mình
                            byte[] aesKey = Backend.Services.CryptoService.DecryptSessionKeyWithRSA(msg.EncryptedSessionKey, AuthService.CurrentPrivateKey);

                            // 2. Dùng khóa AES vừa mở để giải mã nội dung tin nhắn
                            msg.Content = Backend.Services.CryptoService.DecryptWithAES(msg.Content, aesKey);
                        }
                        catch { msg.Content = "[Lỗi giải mã E2EE]"; }
                    }
                    // -----------------------

                    selectedChat.Messages.Add(msg);
                }
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
                    await _signalRService.SendNewChatNotificationAsync(userIds);
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

                // 1. Cập nhật giao diện mượt mà (Vẫn hiển thị chữ thật cho người gửi xem)
                currentChat.Messages.Add(new MessageModel { SenderName = "You", Content = plainText, Time = currentTime, IsMine = true });
                currentChat.LastMessage = "You: " + plainText;
                currentChat.LastMessageTime = currentTime;
                txtMessageInput.Text = "";
                svMessages.ScrollToEnd();

                // 2. LƯU DATABASE BẰNG MÃ HÓA LAI (HYBRID ENCRYPTION)
                try
                {
                    if (!int.TryParse(currentChat.ChatId, out int convId)) return;

                    // --- BƯỚC MẬT MÃ HÓA ---
                    // A. Sinh khóa phiên AES (Session Key) dùng một lần cho tin nhắn này
                    byte[] sessionKey = Backend.Services.CryptoService.GenerateRandomKey();

                    // B. Mã hóa nội dung tin nhắn bằng khóa AES vừa tạo
                    string cipherText = Backend.Services.CryptoService.EncryptWithAES(plainText, sessionKey);

                    // C. Lấy Public Key của tất cả thành viên trong phòng chat
                    var publicKeys = await Backend.Services.ApiClient.GetParticipantPublicKeysAsync(convId);
                    if (publicKeys.Count == 0)
                    {
                        MessageBox.Show("Không thể tải khóa bảo mật của phòng chat!");
                        return;
                    }

                    // D. Bọc (Wrap) khóa AES bằng RSA Public Key của TỪNG NGƯỜI
                    var recipientKeys = new Dictionary<string, string>();
                    foreach (var pk in publicKeys)
                    {
                        string userId = pk.Key;
                        string publicKeyBase64 = pk.Value;

                        recipientKeys[userId] = Backend.Services.CryptoService.EncryptSessionKeyWithRSA(sessionKey, publicKeyBase64);
                    }

                    // E. Gói toàn bộ dữ liệu mã hóa thành DTO
                    var dto = new Hermes.Shared.DTOs.SendMessageDto
                    {
                        ConversationId = convId,
                        SenderId = AuthService.CurrentUserId,
                        CipherText = cipherText, // Gửi chuỗi mã hóa lên Server, tuyệt đối KHÔNG gửi plainText
                        TimeToLive = 0,
                        RecipientSessionKeys = recipientKeys // Các chìa khóa AES đã được khóa chặt bằng RSA
                    };

                    // Gọi API lưu tin nhắn an toàn xuống MySQL
                    bool isSaved = await Backend.Services.ApiClient.SaveMessageAsync(dto);

                    if (isSaved)
                    {
                        // 3. BẮN SIGNALR ĐỂ REAL-TIME
                        // Phát trực tiếp đoạn mã hóa (cipherText) qua WebSocket cho máy bên kia
                        // Truyền thêm recipientKeys vào
                        await _signalRService.SendMessageAsync(currentChat.ChatId, cipherText, recipientKeys);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi hệ thống khi mã hóa E2EE: {ex.Message}", "Lỗi Bảo Mật", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void btnCall_Click(object sender, RoutedEventArgs e)
        {
            if (lstChats.SelectedItem is ChatModel selectedChat)
            {
                if (_isInCall || _isRinging) return; // Không cho gọi đè

                if (!int.TryParse(selectedChat.ChatId, out int convId)) return;
                var participants = await Backend.Services.ApiClient.GetParticipantPublicKeysAsync(convId);
                string targetUserId = participants.Keys.FirstOrDefault(id => id != AuthService.CurrentUserId);

                if (string.IsNullOrEmpty(targetUserId))
                {
                    MessageBox.Show("Không tìm thấy đích đến! (Hiện tại WebRTC P2P chỉ hỗ trợ gọi 1-1).");
                    return;
                }

                _isRinging = true;
                _currentCallTargetId = targetUserId;

                // Hiển thị giao diện đổ chuông
                btnAcceptCall.Visibility = Visibility.Collapsed; // Ẩn nút Nghe vì mình là người gọi
                btnRejectCall.Content = "☎ Cúp máy";
                txtCallStatus.Text = "Đang đổ chuông...";
                CallPopup.Visibility = Visibility.Visible;

                await _webRTCService.InitializeCallAsync();
                await _webRTCService.CreateOfferAsync();
            }
        }

        // Khi mình bấm nút NGHE
        private async void btnAcceptCall_Click(object sender, RoutedEventArgs e)
        {
            _isRinging = false;
            _isInCall = true;

            txtCallStatus.Text = "Đang kết nối...";
            btnAcceptCall.Visibility = Visibility.Collapsed;
            btnRejectCall.Content = "☎ Cúp máy";

            await _webRTCService.InitializeCallAsync();
            await _webRTCService.ReceiveOfferAndCreateAnswerAsync(_incomingOffer);
        }

        // Khi mình bấm nút TỪ CHỐI / CÚP MÁY
        private async void btnRejectCall_Click(object sender, RoutedEventArgs e)
        {
            // Báo cho Server biết để ngắt máy người kia
            await _signalRService.EndCallAsync(_currentCallTargetId);

            // Tự dọn dẹp máy mình
            await HandleEndCallLogic();
        }
    }
}