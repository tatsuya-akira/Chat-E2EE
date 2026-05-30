using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hermes.Backend.Services
{
    public class SignalRService
    {
        private HubConnection _connection;
        public event Action<string, string, Dictionary<string, string>> OnReceiveMessage;
        public event Action<string, string, int> OnIncomingCall;
        public event Action OnNewChatNotification;
        // Thêm Event báo trạng thái Online/Offline
        public event Action<string, bool> OnUserStatusChanged;

        public SignalRService(string hubUrl)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect() // Tự kết nối lại nếu rớt mạng
                .Build();

            _connection.On<string, string, Dictionary<string, string>>("ReceiveMessage", (conversationId, encryptedMessage, recipientKeys) =>
            {
                OnReceiveMessage?.Invoke(conversationId, encryptedMessage, recipientKeys);
            });

            _connection.On<string, string, int>("IncomingCall", (callerId, ip, port) =>
            {
                OnIncomingCall?.Invoke(callerId, ip, port);
            });

            // Lắng nghe trạng thái Online/Offline từ Server
            _connection.On<string, bool>("UserStatusChanged", (userId, isOnline) =>
            {
                OnUserStatusChanged?.Invoke(userId, isOnline);
            });
            // 2. Thêm cái này vào trong Constructor để lắng nghe Server
            _connection.On("ReceiveNewChatNotification", () =>
            {
                OnNewChatNotification?.Invoke();
            });
        }

        public async Task ConnectAsync(string userId)
        {
            try
            {
                await _connection.StartAsync();
                await _connection.InvokeAsync("RegisterUser", userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to SignalR: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.StopAsync();
            }
        }

        public async Task SendMessageAsync(string conversationId, string message, Dictionary<string, string> recipientKeys)
        {
            if (_connection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendMessage", conversationId, message, recipientKeys);
            }
        }

        public async Task InitiateCallAsync(string receiverId, string myIp, int myPort)
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("InitiateCall", receiverId, myIp, myPort);
            }
        }// Thêm hàm lấy danh sách Online lúc mới mở App
        public async Task<List<string>> GetOnlineUsersAsync()
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                return await _connection.InvokeAsync<List<string>>("GetOnlineUsers");
            }
            return new List<string>();
        }
        public async Task JoinRoomAsync(string conversationId)
        {
            if (_connection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("JoinRoom", conversationId);
            }
        }
        public async Task SendNewChatNotificationAsync(List<string> participantIds)
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("NotifyNewChat", participantIds);
            }
        }
    }
}