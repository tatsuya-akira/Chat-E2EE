using System.Collections.Generic;

namespace Hermes.Shared.DTOs
{
    public class SendMessageDto
    {
        public int ConversationId { get; set; }
        public string? SenderId { get; set; }
        public string? CipherText { get; set; } // Nội dung đã mã hóa AES
        public int TimeToLive { get; set; } = 0; // 0 là vĩnh viễn

        // Dictionary lưu trữ: [Id_Người_Nhận] -> [Session_Key_Đã_Mã_Hóa_RSA]
        public Dictionary<string, string>? RecipientSessionKeys { get; set; }
    }
}