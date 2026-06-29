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

        public string Initials { get; set; }
        public string AvatarColor { get; set; } // hex color

        private bool _isOnline = false;
        public bool IsOnline
        {
            get => _isOnline;
            set { if (_isOnline != value) { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(OnlineStatusText)); OnPropertyChanged(nameof(OnlineStatusColor)); } }
        }
        public string OnlineStatusText  => _isOnline ? "Đang hoạt động" : "Không hoạt động";
        public string OnlineStatusColor => _isOnline ? "#10B981" : "#9CA3AF";

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
