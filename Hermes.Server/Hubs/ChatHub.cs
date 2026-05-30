using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Hermes.Server.Hubs
{
    public class ChatHub : Hub
    {
        // Lưu trữ danh sách ConnectionId của mỗi User (1 User có thể có nhiều Connection)
        private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

        // Lưu ngược ConnectionId -> UserId để dọn dẹp nhanh khi có người tắt app (Rớt mạng)
        private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new();

        public async Task RegisterUser(string userId)
        {
            var connectionId = Context.ConnectionId;

            // 1. Thêm vào Map User -> Connections
            _userConnections.AddOrUpdate(userId,
                new HashSet<string> { connectionId },
                (key, existingHashSet) =>
                {
                    lock (existingHashSet) { existingHashSet.Add(connectionId); }
                    return existingHashSet;
                });

            // 2. Thêm vào Map Connection -> User
            _connectionUserMap[connectionId] = userId;

            // 3. Đưa vào Group của SignalR để gửi tin nhắn E2EE
            await Groups.AddToGroupAsync(connectionId, userId);

            // 4. Phát sóng (Broadcast) cho toàn bộ người dùng khác biết tài khoản này vừa Online
            await Clients.Others.SendAsync("UserStatusChanged", userId, true);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            // Nếu user rớt mạng hoặc tắt app, tìm xem đó là ai
            if (_connectionUserMap.TryRemove(connectionId, out string userId))
            {
                if (_userConnections.TryGetValue(userId, out var connections))
                {
                    lock (connections) { connections.Remove(connectionId); }

                    // Nếu user không còn Connection nào (đã tắt app trên tất cả thiết bị)
                    if (connections.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);

                        // Phát sóng trạng thái Offline
                        await Clients.Others.SendAsync("UserStatusChanged", userId, false);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Lấy danh sách các user đang Online (Dùng khi 1 user mới đăng nhập vào app)
        public async Task<List<string>> GetOnlineUsers()
        {
            return _userConnections.Keys.ToList();
        }

        // Hàm cho phép Client chui vào một phòng chat cụ thể
        public async Task JoinRoom(string conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        }

        // --- CÁC HÀM CŨ GIỮ NGUYÊN ---
        public async Task SendMessage(string conversationId, string encryptedMessage)
        {
            // FIX TẠI ĐÂY: Đổi chữ 'Group' thành 'OthersInGroup'
            await Clients.OthersInGroup(conversationId).SendAsync("ReceiveMessage", conversationId, encryptedMessage);
        }

        public async Task InitiateCall(string receiverId, string myIp, int myPort)
        {
            await Clients.Group(receiverId).SendAsync("IncomingCall", Context.ConnectionId, myIp, myPort);
        }
    }
}