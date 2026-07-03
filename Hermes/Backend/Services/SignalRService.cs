using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hermes.Backend.Services
{
    public class SignalRService
    {
        private HubConnection _connection;
        public event Action<string, string, Dictionary<string, string>, int, int> OnReceiveMessage;
        public event Action<string, string, int> OnIncomingCall;
        public event Action OnNewChatNotification;
        public event Action<string, string> OnReceiveWebRTCOffer; // Nhận lời mời (Kèm ID người gọi)
        public event Action<string> OnReceiveWebRTCAnswer;        // Nhận phản hồi
        public event Action<string> OnReceiveIceCandidate;        // Nhận IP mặt tiền
        public event Action OnCallEnded;                          // Cúp máy
        public event Action<string, bool> OnUserStatusChanged;
        public event Action<string, int> OnReceiveMessageDeletion;
        public event Action<string, string> OnMessagesMarkedAsRead;

        public SignalRService(string hubUrl)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect() // Tự kết nối lại nếu rớt mạng
                .Build();

            _connection.On<string, string, Dictionary<string, string>, int, int>("ReceiveMessage", (conversationId, encryptedMessage, recipientKeys, ttl, msgId) =>
            {
                OnReceiveMessage?.Invoke(conversationId, encryptedMessage, recipientKeys, ttl, msgId);
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

            _connection.On<string, int>("ReceiveMessageDeletion", (conversationId, messageId) =>
            {
                OnReceiveMessageDeletion?.Invoke(conversationId, messageId);
            });
            // 2. Thêm cái này vào trong Constructor để lắng nghe Server
            _connection.On("ReceiveNewChatNotification", () =>
            {
                OnNewChatNotification?.Invoke();
            });
            _connection.On<string, string>("ReceiveWebRTCOffer", (callerId, offer) => OnReceiveWebRTCOffer?.Invoke(callerId, offer));
            _connection.On<string>("ReceiveWebRTCAnswer", (answer) => OnReceiveWebRTCAnswer?.Invoke(answer));
            _connection.On<string>("ReceiveIceCandidate", (candidate) => OnReceiveIceCandidate?.Invoke(candidate));
            _connection.On("CallEnded", () => OnCallEnded?.Invoke());
            _connection.On<string, string>("MessagesMarkedAsRead", (conversationId, userId) => OnMessagesMarkedAsRead?.Invoke(conversationId, userId));
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

        public async Task SendMessageAsync(string conversationId, string message, Dictionary<string, string> recipientKeys, int ttl = 0, int msgId = 0)
        {
            if (_connection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendMessage", conversationId, message, recipientKeys, ttl, msgId);
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
        public async Task SendWebRTCOfferAsync(string targetId, string offer) => await _connection.InvokeAsync("SendWebRTCOffer", targetId, offer);
        public async Task SendWebRTCAnswerAsync(string targetId, string answer) => await _connection.InvokeAsync("SendWebRTCAnswer", targetId, answer);
        public async Task SendIceCandidateAsync(string targetId, string candidate) => await _connection.InvokeAsync("SendIceCandidate", targetId, candidate);
        public async Task EndCallAsync(string targetId) => await _connection.InvokeAsync("EndCall", targetId);

        public async Task NotifyDeleteMessageAsync(string conversationId, int messageId)
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("NotifyDeleteMessage", conversationId, messageId);
            }
        }
        public async Task NotifyMessagesReadAsync(string conversationId, string userId)
        {
            // Đây là lệnh gọi trực tiếp hàm "NotifyMessagesRead" trên Server
            await _connection.InvokeAsync("NotifyMessagesRead", conversationId, userId);
        }
    }
}