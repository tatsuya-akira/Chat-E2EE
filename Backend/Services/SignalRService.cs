using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace Hermes.Backend.Services
{
    public class SignalRService
    {
        private HubConnection _connection;
        public event Action<string, string> OnReceiveMessage;
        public event Action<string, string, int> OnIncomingCall;

        public SignalRService(string hubUrl)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveMessage", (senderId, encryptedMessage) =>
            {
                OnReceiveMessage?.Invoke(senderId, encryptedMessage);
            });

            _connection.On<string, string, int>("IncomingCall", (callerId, ip, port) =>
            {
                OnIncomingCall?.Invoke(callerId, ip, port);
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

        public async Task SendMessageAsync(string receiverId, string encryptedMessage)
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("SendMessage", receiverId, encryptedMessage);
            }
        }

        public async Task InitiateCallAsync(string receiverId, string myIp, int myPort)
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("InitiateCall", receiverId, myIp, myPort);
            }
        }
    }
}
