using Microsoft.AspNetCore.SignalR;

namespace Hermes.Server.Hubs
{
    public class ChatHub : Hub
    {
        public async Task RegisterUser(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }

        public async Task SendMessage(string receiverId, string encryptedMessage)
        {
            await Clients.Group(receiverId).SendAsync("ReceiveMessage", Context.ConnectionId, encryptedMessage);
        }

        public async Task InitiateCall(string receiverId, string myIp, int myPort)
        {
            await Clients.Group(receiverId).SendAsync("IncomingCall", Context.ConnectionId, myIp, myPort);
        }
    }
}
