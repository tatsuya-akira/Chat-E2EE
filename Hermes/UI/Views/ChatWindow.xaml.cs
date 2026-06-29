// Standardized to production level
// Purpose: ChatWindow code-behind – real-time chat + live user search
// Dependencies: Backend.Services, Hermes.Shared.Models, SignalRService, WebRTCService
using Hermes.Backend.Services;
using Hermes.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // ===== SEARCH FIELDS =====
        private CancellationTokenSource _searchCts;
        private ObservableCollection<SearchResultItem> _searchResults;
        private bool _isSearchPlaceholderVisible = true;
        public ChatWindow()
        {
            InitializeComponent();
            Chats = new ObservableCollection<ChatModel>();
            lstChats.ItemsSource = Chats;

            // Khởi tạo search trước khi bất kỳ sự kiện nào fire
            InitializeSearch();

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
            if (myChats == null) return;

            // --- GỘP (MERGE) thay vì Clear + Add toàn bộ ---
            // Bước 1: Xóa các chat không còn trên server
            var serverIds = new HashSet<string>(myChats
                .Where(c => c?.ChatId != null)
                .Select(c => c.ChatId));
            var toRemove = Chats.Where(ch => ch?.ChatId != null && !serverIds.Contains(ch.ChatId)).ToList();
            foreach (var obsolete in toRemove)
                Chats.Remove(obsolete);

            // Bước 2: Thêm mới hoặc cập nhật từng chat từ server
            foreach (var c in myChats)
            {
                if (c == null || string.IsNullOrEmpty(c.ChatId)) continue;

                string displayName = c.IsGroup ? (c.GroupName ?? "") : (c.OtherUserName ?? "");
                var incoming = new ChatModel
                {
                    ChatId          = c.ChatId,
                    ChatName        = displayName,
                    Initials        = c.IsGroup ? "G" : (string.IsNullOrEmpty(displayName) ? "" : displayName.Substring(0, 1).ToUpper()),
                    AvatarColor     = c.IsGroup ? "#10B981" : "#F59E0B",
                    LastMessage     = "Bấm để xem tin nhắn...",
                    LastMessageTime = ""
                };

                AddOrUpdateChat(incoming, joinRoom: false);
                await _signalRService.JoinRoomAsync(c.ChatId);
            }

            if (Chats.Any() && lstChats.SelectedIndex < 0)
                lstChats.SelectedIndex = 0;
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

        /// <summary>
        /// Wire up MenuItem.Click khi ContextMenu mở ra – bắt buộc phải làm trong code-behind
        /// vì Click= trực tiếp trong XAML Style/ContextMenu scope gây InvalidCastException tại InitializeComponent.
        /// </summary>
        private void lstChats_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Duyệt lên visual tree để tìm ListBoxItem chứa ContextMenu
            var source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is System.Windows.Controls.ListBoxItem))
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);

            if (source is not System.Windows.Controls.ListBoxItem lbi) return;
            if (lbi.ContextMenu is not ContextMenu ctxMenu) return;

            // Đảm bảo DataContext của ContextMenu khớp với item
            ctxMenu.DataContext = lbi.DataContext;
            ctxMenu.PlacementTarget = lbi;

            foreach (var obj in ctxMenu.Items)
            {
                if (obj is MenuItem mi)
                {
                    // Re-attach tránh duplicate subscription
                    mi.Click -= menuDeleteChat_Click;
                    mi.Click += menuDeleteChat_Click;
                    // Gán Tag trực tiếp = DataContext của ListBoxItem (ChatModel)
                    mi.Tag = lbi.DataContext;
                }
            }
        }

        /// <summary>
        /// Xóa đoạn chat khỏi danh sách hiển thị (chuột phải → Xóa đoạn chat).
        /// Chỉ xóa trên UI – không gọi API xóa dữ liệu server (an toàn cho E2EE).
        /// </summary>
        private void menuDeleteChat_Click(object sender, RoutedEventArgs e)

        {
            // NULL GUARD
            if (sender is not System.Windows.Controls.MenuItem menuItem) return;
            if (Chats == null) return;

            // Lấy ChatModel từ DataContext của item (Tag bind đến DataContext)
            ChatModel chatToDelete = null;

            // MenuItem.Tag = {Binding} → DataContext của ListBoxItem
            if (menuItem.Tag is ChatModel tagModel)
            {
                chatToDelete = tagModel;
            }
            else
            {
                // Fallback: lấy từ ContextMenu.PlacementTarget → ListBoxItem → DataContext
                if (menuItem.Parent is ContextMenu ctxMenu &&
                    ctxMenu.PlacementTarget is System.Windows.Controls.ListBoxItem lbi &&
                    lbi.DataContext is ChatModel lbiModel)
                {
                    chatToDelete = lbiModel;
                }
            }

            if (chatToDelete == null) return;

            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa cuộc trò chuyện «{chatToDelete.ChatName}» khỏi danh sách?\n\n" +
                "Lịch sử tin nhắn được mã hóa E2EE vẫn được giữ an toàn trên server.",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Nếu đang xem chat đó → reset về màn hình chào
            if (lstChats?.SelectedItem is ChatModel selected && selected.ChatId == chatToDelete.ChatId)
            {
                if (GridWelcome != null) GridWelcome.Visibility = Visibility.Visible;
                if (GridChat    != null) GridChat.Visibility    = Visibility.Collapsed;
                lstChats.SelectedItem = null;
            }

            Chats.Remove(chatToDelete);
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
                        ChatId          = newConvId.ToString(),
                        ChatName        = newChatName,
                        Initials        = createChat.IsGroup ? "G" : (newChatName.Length > 0 ? newChatName.Substring(0, 1).ToUpper() : ""),
                        AvatarColor     = createChat.IsGroup ? "#10B981" : "#F59E0B",
                        LastMessage     = "Bắt đầu cuộc trò chuyện...",
                        LastMessageTime = DateTime.Now.ToString("hh:mm tt")
                    };
                    // DEDUP: chỉ thêm nếu ChatId chưa tồn tại trong danh sách
                    var inserted = AddOrUpdateChat(newChat, joinRoom: false);
                    if (inserted != null) lstChats.SelectedItem = inserted;
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

        // ===================================================================
        // ===== LIVE USER SEARCH – LOGIC TÌM KIẾM NGƯỜI DÙNG REAL-TIME =====
        // ===================================================================

        /// <summary>
        /// Khởi tạo datasource cho search results – gọi 1 lần sau InitializeComponent
        /// </summary>
        private void InitializeSearch()
        {
            _searchResults = new ObservableCollection<SearchResultItem>();
            if (lstSearchResults != null)
                lstSearchResults.ItemsSource = _searchResults;
        }

        /// <summary>
        /// Sự kiện TextChanged – debounce 400ms để tránh spam API
        /// CRITICAL: Luôn kiểm tra null trước khi truy cập UI controls
        /// </summary>
        private async void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // NULL GUARD – TextChanged có thể fire trước khi XAML render xong
            if (txtSearch == null || popupSearchResults == null ||
                lstSearchResults == null || _searchResults == null)
                return;

            string keyword = txtSearch.Text?.Trim() ?? string.Empty;

            // Quản lý placeholder
            if (txtSearchPlaceholder != null)
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(keyword)
                    ? Visibility.Visible : Visibility.Collapsed;

            // Nếu trống → đóng popup và thoát
            if (string.IsNullOrEmpty(keyword))
            {
                CloseSearchPopup();
                return;
            }

            // Hủy request cũ nếu đang chạy (debounce)
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            // Hiện trạng thái "đang tìm..."
            ShowSearchStatus("🔍 Đang tìm kiếm...");
            EnsurePopupOpen();

            try
            {
                // Debounce 400ms
                await Task.Delay(400, token);
                if (token.IsCancellationRequested) return;

                // Gọi API tìm kiếm
                var userInfo = await Backend.Services.ApiClient.GetUserByIdentifierAsync(keyword);
                if (token.IsCancellationRequested) return;

                // Cập nhật UI trên Dispatcher thread
                Dispatcher.Invoke(() =>
                {
                    if (popupSearchResults == null || _searchResults == null) return;

                    _searchResults.Clear();

                    if (userInfo != null)
                    {
                        // UserInfoResponse chỉ có UserId và FullName
                        string displayName = !string.IsNullOrWhiteSpace(userInfo.FullName)
                            ? userInfo.FullName
                            : userInfo.UserId ?? "Người dùng";

                        string initials = displayName.Length > 0
                            ? displayName.Substring(0, 1).ToUpper()
                            : "?";

                        // Tạo màu avatar ngẫu nhiên dựa trên hash của tên
                        string[] palette = { "#3B82F6", "#8B5CF6", "#EC4899", "#10B981", "#F59E0B", "#EF4444" };
                        string avatarColor = palette[Math.Abs(displayName.GetHashCode()) % palette.Length];

                        _searchResults.Add(new SearchResultItem
                        {
                            UserId      = userInfo.UserId ?? "",
                            DisplayName = displayName,
                            Identifier  = userInfo.UserId ?? keyword,   // Hiển thị identifier nhập vào
                            Initials    = initials,
                            AvatarColor = avatarColor
                        });

                        HideNoResult();
                        HideSearchStatus();
                    }
                    else
                    {
                        ShowNoResult();
                        HideSearchStatus();
                    }

                    EnsurePopupOpen();
                });
            }
            catch (OperationCanceledException)
            {
                // Bị hủy do người dùng tiếp tục gõ – bình thường, bỏ qua
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowSearchStatus($"⚠ Lỗi tìm kiếm: {ex.Message}");
                    HideNoResult();
                });
            }
        }

        /// <summary>
        /// Khi người dùng chọn một kết quả trong danh sách gợi ý
        /// → Tự động mở/tạo cuộc trò chuyện 1-1 với người đó
        /// </summary>
        private async void lstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSearchResults == null) return;
            if (lstSearchResults.SelectedItem is not SearchResultItem selected) return;

            // Reset selection ngay để không bị kẹt highlight
            lstSearchResults.SelectedItem = null;

            CloseSearchPopup();
            ResetSearchBox();

            // Kiểm tra xem đã có conversation 1-1 với người này chưa
            var existing = Chats.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.ChatName) &&
                c.ChatName.Equals(selected.DisplayName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Đã có → chỉ cần focus vào conversation đó
                lstChats.SelectedItem = existing;
                return;
            }

            // Chưa có → tạo conversation mới
            try
            {
                var userIds = new List<string> { AuthService.CurrentUserId, selected.UserId };
                int newConvId = await Backend.Services.ApiClient.CreateConversationAsync(
                    isGroup: false,
                    groupName: null,
                    userIds: userIds);

                if (newConvId == -1)
                {
                    MessageBox.Show("Không thể tạo cuộc trò chuyện. Vui lòng thử lại.",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newChat = new ChatModel
                {
                    ChatId          = newConvId.ToString(),
                    ChatName        = selected.DisplayName,
                    Initials        = selected.Initials,
                    AvatarColor     = selected.AvatarColor,
                    LastMessage     = "Bắt đầu cuộc trò chuyện...",
                    LastMessageTime = DateTime.Now.ToString("hh:mm tt")
                };

                // DEDUP: chỉ thêm nếu ChatId chưa tồn tại
                var inserted = AddOrUpdateChat(newChat, joinRoom: false);
                if (inserted != null) lstChats.SelectedItem = inserted;
                await _signalRService.JoinRoomAsync(newConvId.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi tạo cuộc trò chuyện",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearchPlaceholder != null)
                txtSearchPlaceholder.Visibility = Visibility.Collapsed;

            if (txtSearch != null)
                txtSearch.Foreground = System.Windows.Media.Brushes.Black;

            // Nếu đang có kết quả → hiện lại popup
            if (_searchResults != null && _searchResults.Count > 0)
                EnsurePopupOpen();
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            // Delay nhỏ để cho phép click vào item trong popup trước khi đóng
            Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (txtSearch == null) return;
                if (string.IsNullOrEmpty(txtSearch.Text))
                {
                    if (txtSearchPlaceholder != null)
                        txtSearchPlaceholder.Visibility = Visibility.Visible;
                    txtSearch.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }));
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResetSearchBox();
                CloseSearchPopup();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Di chuyển focus xuống danh sách
                if (lstSearchResults != null && lstSearchResults.Items.Count > 0)
                {
                    lstSearchResults.Focus();
                    lstSearchResults.SelectedIndex = 0;
                    e.Handled = true;
                }
            }
        }

        // ===== HELPER METHODS – NULL-SAFE =====

        /// <summary>
        /// Thêm chat vào danh sách NẾU ChatId chưa tồn tại.
        /// Nếu đã tồn tại thì chỉ cập nhật LastMessage + LastMessageTime (không tạo dòng mới).
        /// Trả về item thực sự trong danh sách (mới hoặc đã có) để caller có thể SelectedItem.
        /// </summary>
        private ChatModel? AddOrUpdateChat(ChatModel incoming, bool joinRoom)
        {
            // NULL GUARD
            if (incoming == null || string.IsNullOrEmpty(incoming.ChatId)) return null;
            if (Chats == null) return null;

            // Tìm xem ChatId đã tồn tại chưa
            var existing = Chats.FirstOrDefault(ch => ch?.ChatId == incoming.ChatId);

            if (existing != null)
            {
                // ĐÃ CÓ → chỉ cập nhật nội dung hiển thị, KHÔNG thêm dòng mới
                if (!string.IsNullOrEmpty(incoming.LastMessage))
                    existing.LastMessage = incoming.LastMessage;
                if (!string.IsNullOrEmpty(incoming.LastMessageTime))
                    existing.LastMessageTime = incoming.LastMessageTime;
                return existing;
            }

            // CHƯA CÓ → chèn lên đầu danh sách
            Chats.Insert(0, incoming);
            return incoming;
        }

        private void EnsurePopupOpen()
        {
            if (popupSearchResults != null && !popupSearchResults.IsOpen)
                popupSearchResults.IsOpen = true;
        }

        private void CloseSearchPopup()
        {
            if (popupSearchResults != null)
                popupSearchResults.IsOpen = false;
        }

        private void ShowSearchStatus(string message)
        {
            if (txtSearchStatus == null) return;
            txtSearchStatus.Text = message;
            txtSearchStatus.Visibility = Visibility.Visible;
            if (panelNoResult != null) panelNoResult.Visibility = Visibility.Collapsed;
            if (lstSearchResults != null) lstSearchResults.Visibility = Visibility.Collapsed;
        }

        private void HideSearchStatus()
        {
            if (txtSearchStatus != null) txtSearchStatus.Visibility = Visibility.Collapsed;
            if (lstSearchResults != null) lstSearchResults.Visibility = Visibility.Visible;
        }

        private void ShowNoResult()
        {
            if (panelNoResult != null) panelNoResult.Visibility = Visibility.Visible;
            if (lstSearchResults != null) lstSearchResults.Visibility = Visibility.Collapsed;
        }

        private void HideNoResult()
        {
            if (panelNoResult != null) panelNoResult.Visibility = Visibility.Collapsed;
            if (lstSearchResults != null) lstSearchResults.Visibility = Visibility.Visible;
        }

        private void ResetSearchBox()
        {
            if (txtSearch == null) return;
            txtSearch.Text = string.Empty;
            _searchResults?.Clear();
            if (txtSearchPlaceholder != null)
                txtSearchPlaceholder.Visibility = Visibility.Visible;
            txtSearch.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    /// <summary>
    /// ViewModel nhẹ dùng để bind vào danh sách kết quả tìm kiếm
    /// </summary>
    public class SearchResultItem
    {
        public string UserId      { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Identifier  { get; set; } = string.Empty;
        public string Initials    { get; set; } = string.Empty;
        public string AvatarColor { get; set; } = "#3B82F6";
    }
}