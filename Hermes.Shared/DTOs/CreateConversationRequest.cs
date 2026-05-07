using System.Collections.Generic;

namespace Hermes.Shared.DTOs
{
    public class CreateConversationRequest
    {
        public bool IsGroup { get; set; }
        public string? GroupName { get; set; }
        public List<string> ParticipantIds { get; set; }
    }
}