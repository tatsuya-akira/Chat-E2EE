using System.Text.Json.Serialization;
using Hermes.Shared.Converters;

namespace Hermes.Shared.DTOs
{
    public class ChatListResponse
    {
        public string? ChatId { get; set; }
        public bool IsGroup { get; set; }
        public string? GroupName { get; set; }
        public string? OtherUserName { get; set; }
        public string? OtherUserId { get; set; }
        [JsonConverter(typeof(NumberToBooleanConverter))]
        public bool IsRead { get; set; } = true;
    }
}