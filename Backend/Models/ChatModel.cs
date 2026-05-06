using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hermes.Models
{
    public class MessageModel : INotifyPropertyChanged
    {
        private string _senderName;
        private string _content;
        private string _time;
        private bool _isMine;
        private bool _isSeen;
        private long _messageId;

        public long MessageId
        {
            get => _messageId;
            set { _messageId = value; OnPropertyChanged(); }
        }

        public string SenderName
        {
            get => _senderName;
            set { _senderName = value; OnPropertyChanged(); }
        }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public string Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }

        public bool IsMine
        {
            get => _isMine;
            set { _isMine = value; OnPropertyChanged(); }
        }

        public bool IsSeen
        {
            get => _isSeen;
            set { _isSeen = value; OnPropertyChanged(); }
        }

        public string SeenIcon => IsMine ? (IsSeen ? "✓✓" : "✓") : "";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ChatModel : INotifyPropertyChanged
    {
        private string _chatId;
        private string _chatName;
        private string _lastMessage;
        private string _lastMessageTime;
        private string _initials;
        private string _avatarColor;
        private string _avatarUrl;
        private bool _isOnline;
        private ObservableCollection<MessageModel> _messages;

        public string ChatId
        {
            get => _chatId;
            set { _chatId = value; OnPropertyChanged(); }
        }

        public string ChatName
        {
            get => _chatName;
            set { _chatName = value; OnPropertyChanged(); }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set { _lastMessage = value; OnPropertyChanged(); }
        }

        public string LastMessageTime
        {
            get => _lastMessageTime;
            set { _lastMessageTime = value; OnPropertyChanged(); }
        }

        public string Initials
        {
            get => _initials;
            set { _initials = value; OnPropertyChanged(); }
        }

        public string AvatarColor
        {
            get => _avatarColor;
            set { _avatarColor = value; OnPropertyChanged(); }
        }

        /// <summary>Remote URL or local file path for real avatar image.</summary>
        public string AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAvatar)); }
        }

        public bool HasAvatar => !string.IsNullOrEmpty(_avatarUrl);

        /// <summary>True when this contact is currently online (Firebase presence).</summary>
        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        public ObservableCollection<MessageModel> Messages
        {
            get => _messages;
            set { _messages = value; OnPropertyChanged(); }
        }

        public ChatModel()
        {
            Messages = new ObservableCollection<MessageModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
