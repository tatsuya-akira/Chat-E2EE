using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hermes.Shared.Models
{
    public class MessageModel : INotifyPropertyChanged
    {
        public int MessageId { get; set; }
        public string? EncryptedSessionKey { get; set; }
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }

        private string? _content;
        public string? Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayContent));
                }
            }
        }
        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead != value)
                {
                    _isRead = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ReadStatusColor));
                }
            }
        }

        public string ReadStatusColor => IsRead ? "#3B82F6" : "#9CA3AF";

        public string? Time { get; set; }
        public bool IsMine { get; set; }

        private int _timeToLive = 0;
        public int TimeToLive
        {
            get => _timeToLive;
            set
            {
                if (_timeToLive != value)
                {
                    _timeToLive = value;
                    if (_remainingTime == 0)
                    {
                        RemainingTime = value;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayContent));
                }
            }
        }

        private int _remainingTime;
        public int RemainingTime
        {
            get => _remainingTime;
            set
            {
                if (_remainingTime != value)
                {
                    _remainingTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayContent));
                }
            }
        }

        public string DisplayContent
        {
            get
            {
                if (TimeToLive == -1)
                {
                    return IsMine ? $"🔒 [Xem 1 lần] {Content}" : "🔒 [Tin nhắn xem 1 lần - Nhấn vào đây để xem]";
                }
                if (TimeToLive > 0)
                {
                    return $"{Content}\n⏳ Tự hủy sau: {RemainingTime}s";
                }
                return Content ?? "";
            }
        }

        public void StartCountdown(System.Action<MessageModel> onExpired)
        {
            if (TimeToLive <= 0) return;
            RemainingTime = TimeToLive;
            System.Threading.Tasks.Task.Run(async () =>
            {
                while (RemainingTime > 0)
                {
                    await System.Threading.Tasks.Task.Delay(1000);
                    RemainingTime--;
                }
                onExpired?.Invoke(this);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ChatModel : INotifyPropertyChanged
    {
        public string ChatId { get; set; }
        public string ChatName { get; set; }

        private string _lastMessage;
        public string LastMessage
        {
            get => _lastMessage;
            set { if (_lastMessage != value) { _lastMessage = value; OnPropertyChanged(); } }
        }

        private string _lastMessageTime;
        public string LastMessageTime
        {
            get => _lastMessageTime;
            set { if (_lastMessageTime != value) { _lastMessageTime = value; OnPropertyChanged(); } }
        }
        public bool IsGroup { get; set; }
        public string Initials { get; set; }
        public string AvatarColor { get; set; } // hex color

        public string? TargetUserId { get; set; } // Giữ ID đối phương
        private bool _isOnline;
        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }
        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set { _isRead = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReadStatusColor)); }
        }
        public string ReadStatusColor => IsRead ? "#3B82F6" : "#9CA3AF"; // Xanh (Đã xem), Xám (Đã nhận)
        public string StatusText => IsOnline ? "Đang hoạt động" : "Ngoại tuyến";
        public string StatusColor => IsOnline ? "#10B981" : "#9CA3AF"; // Xanh ngọc hoặc Xám

        public ObservableCollection<MessageModel> Messages { get; set; }

        public ChatModel()
        {
            Messages = new ObservableCollection<MessageModel>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
