using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hermes.Models
{
    public class MessageModel : INotifyPropertyChanged
    {
        public string SenderName { get; set; }
        public string Content { get; set; }
        public string Time { get; set; }
        public bool IsMine { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ChatModel : INotifyPropertyChanged
    {
        public string ChatId { get; set; }
        public string ChatName { get; set; }
        public string LastMessage { get; set; }
        public string LastMessageTime { get; set; }
        public string Initials { get; set; }
        public string AvatarColor { get; set; } // hex color

        public ObservableCollection<MessageModel> Messages { get; set; }

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
