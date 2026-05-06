using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Hermes.Backend.Services
{
    public class VoiceService : IDisposable
    {
        private UdpClient _udpClient;
        private WaveInEvent _waveIn;
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _waveProvider;
        private IPEndPoint _remoteEndPoint;
        private CancellationTokenSource _cts;

        public event Action<string> OnError;

        public async Task<(string PublicIp, int PublicPort)> GetPublicEndPointFromStunAsync()
        {
            await Task.Delay(100);
            return ("127.0.0.1", 50000); 
        }

        public void InitializeCall(string remoteIp, int remotePort)
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            _udpClient = new UdpClient(0); // Bind to any available local port
            _cts = new CancellationTokenSource();

            // Hole Punching
            HolePunchAsync().ConfigureAwait(false);

            // Audio setup
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, Mono as per CONTEXT.md
            };
            _waveIn.DataAvailable += WaveIn_DataAvailable;

            _waveProvider = new BufferedWaveProvider(_waveIn.WaveFormat);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_waveProvider);
        }

        private async Task HolePunchAsync()
        {
            try
            {
                byte[] dummyPacket = new byte[] { 0x00 };
                for (int i = 0; i < 3; i++)
                {
                    await _udpClient.SendAsync(dummyPacket, dummyPacket.Length, _remoteEndPoint);
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Hole Punching error: {ex.Message}");
            }
        }

        public void StartCall()
        {
            _waveIn?.StartRecording();
            _waveOut?.Play();
            Task.Run(() => ReceiveAudioLoop(_cts.Token));
        }

        public void EndCall()
        {
            _cts?.Cancel();
            _waveIn?.StopRecording();
            _waveOut?.Stop();
        }

        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (_udpClient != null && _remoteEndPoint != null)
                {
                    await _udpClient.SendAsync(e.Buffer, e.BytesRecorded, _remoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send audio error: {ex.Message}");
            }
        }

        private async Task ReceiveAudioLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpClient.ReceiveAsync(token);
                    if (result.Buffer.Length > 1) // ignore dummy hole punch packets
                    {
                        _waveProvider?.AddSamples(result.Buffer, 0, result.Buffer.Length);
                    }
                }
            }
            catch (OperationCanceledException) { /* Call Ended */ }
            catch (Exception ex)
            {
                OnError?.Invoke($"Receive audio error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            EndCall();
            _waveIn?.Dispose();
            _waveOut?.Dispose();
            _udpClient?.Dispose();
            _cts?.Dispose();
        }
    }
}