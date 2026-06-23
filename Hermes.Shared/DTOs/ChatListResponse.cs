namespace Hermes.Shared.DTOs
{
    public class ChatListResponse
    {
        public string ChatId { get; set; }
        public bool IsGroup { get; set; }
        public string GroupName { get; set; }
        public string OtherUserName { get; set; } // Dùng cho chat 1-1
    }
}