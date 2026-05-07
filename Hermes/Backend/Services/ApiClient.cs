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
            _httpClient.BaseAddress = new Uri("http://localhost:5042/api/");
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
        public static async Task<bool> SaveMessageAsync(SendMessageDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync("Message/save", dto);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                System.Windows.MessageBox.Show($"Lỗi lưu tin nhắn: {error}");
                return false;
            }
            return true; // Lưu Database thành công!
        }
        public static async Task<List<MessageModel>> GetChatHistoryAsync(int conversationId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"Message/history/{conversationId}");
                if (response.IsSuccessStatusCode)
                {
                    // Map từ DTO Server về Model hiển thị của WPF
                    var history = await response.Content.ReadFromJsonAsync<List<MessageModel>>();
                    return history ?? new List<MessageModel>();
                }
            }
            catch (Exception ex) { /* Log lỗi */ }
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
    }
}