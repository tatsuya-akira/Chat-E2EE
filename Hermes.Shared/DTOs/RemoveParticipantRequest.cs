namespace Hermes.Shared.DTOs
{
    public class RemoveParticipantRequest
    {
        public int ConversationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // "LEAVE" or "KICK"
    }
}
