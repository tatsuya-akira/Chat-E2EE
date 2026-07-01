using Hermes.Shared.DTOs;
using Hermes.Shared.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hermes.Backend.Services
{
    public class ApiClient
    {
        private static readonly HttpClient _httpClient;

        static ApiClient()
        {
            _httpClient = new HttpClient();
            // TODO: Load from AppSettings or .env
            _httpClient.BaseAddress = new Uri("http://127.0.0.1:5042/api/");
        }

        public static async Task<bool> CheckIdentifierExistsAsync(string identifier)
        {
            var response = await _httpClient.GetAsync($"Auth/check-identifier?identifier={identifier}");
            return !response.IsSuccessStatusCode; // if not success (e.g. 400 BadRequest), it means it exists
        }

        public static async Task<bool> RegisterUserAsync(RegisterRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("Auth/register", request);
            return response.IsSuccessStatusCode;
        }

        public static async Task<UserKeysResponse> GetUserKeysAsync(string userId)
        {
            return await _httpClient.GetFromJsonAsync<UserKeysResponse>($"Auth/keys/{userId}");
        }
        public static async Task<UserInfoResponse> GetUserByIdentifierAsync(string identifier)
        {
            var response = await _httpClient.GetAsync($"Conversation/user/{identifier}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<UserInfoResponse>();
            return null;
        }

        public static async Task<List<UserInfoResponse>> SearchUsersAsync(string keyword)
        {
            var response = await _httpClient.GetAsync($"Conversation/search-users?keyword={Uri.EscapeDataString(keyword ?? "")}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<UserInfoResponse>>() ?? new List<UserInfoResponse>();
            return new List<UserInfoResponse>();
        }

        public static async Task<int> CreateConversationAsync(bool isGroup, string groupName, List<string> userIds)
        {
            var request = new CreateConversationRequest { IsGroup = isGroup, GroupName = groupName, ParticipantIds = userIds };
            var response = await _httpClient.PostAsJsonAsync("Conversation", request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<int>();
            }
            else
            {
                string errorDetail = await response.Content.ReadAsStringAsync();
                System.Windows.MessageBox.Show($"Mã lỗi HTTP: {response.StatusCode}\nChi tiết Server trả về: {errorDetail}", "Debug từ API");
                return -1;
            }
        }
        public static async Task<int> SaveMessageAsync(SendMessageDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync("Message/save", dto);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                System.Windows.MessageBox.Show($"Lỗi lưu tin nhắn: {error}");
                return -1;
            }
            var resStr = await response.Content.ReadAsStringAsync();
            if (int.TryParse(resStr, out int msgId))
            {
                return msgId;
            }
            return 1;
        }

        public static async Task<bool> DeleteMessageAsync(int messageId)
        {
            var response = await _httpClient.DeleteAsync($"Message/{messageId}");
            return response.IsSuccessStatusCode;
        }
        public static async Task<List<MessageModel>> GetChatHistoryAsync(int conversationId, string userId)
        {
            // Thêm userId vào URL
            var response = await _httpClient.GetAsync($"Message/history/{conversationId}/{userId}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MessageModel>>() ?? new List<MessageModel>();
            }
            else
            {
                // MÁY QUÉT LỖI: Nếu API thất bại, nó sẽ pop-up lên báo cho bạn biết ngay!
                string errorDetail = await response.Content.ReadAsStringAsync();
                System.Windows.MessageBox.Show($"Không thể tải lịch sử!\nHTTP Status: {response.StatusCode}\nChi tiết: {errorDetail}", "Debug từ API Server");
            }

            return new List<MessageModel>();
        }
        public static async Task<List<ChatListResponse>> GetMyChatsAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"Conversation/my-chats/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<ChatListResponse>>() ?? new List<ChatListResponse>();
                }
            }
            catch { /* Xử lý lỗi mạng */ }
            return new List<ChatListResponse>();
        }
        public static async Task<Dictionary<string, string>> GetParticipantPublicKeysAsync(int conversationId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"Conversation/{conversationId}/public-keys");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                }
            }
            catch { /* Lỗi mạng */ }

            return new Dictionary<string, string>();
        }
        public static async Task<string> GetUsernameAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"Conversation/username/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync() ?? "Unknown";
                }
            }
            catch { /* Lỗi mạng */ }
            return "Unknown";
        }
        public static async Task<bool> UpdateUserKeysAsync(UpdateKeyRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync("Auth/update-keys", request);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> RemoveParticipantAsync(int conversationId, string userId, string actionType)
        {
            try
            {
                var req = new Hermes.Shared.DTOs.RemoveParticipantRequest
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    ActionType = actionType
                };
                var response = await _httpClient.PostAsJsonAsync("Conversation/remove-participant", req);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}