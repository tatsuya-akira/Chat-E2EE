using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hermes.Shared.Models
{
    public class MessageModel : INotifyPropertyChanged
    {
        public string? EncryptedSessionKey { get; set; }
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }
        public string? Content { get; set; }
        public string? Time { get; set; }
        public bool IsMine { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
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
