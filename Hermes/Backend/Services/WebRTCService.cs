using NAudio.Codecs;
using NAudio.Wave;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Hermes.Client.Services
{
    public class WebRTCService
    {
        private RTCPeerConnection _peerConnection;

        // ==========================================
        // VŨ KHÍ TỐI THƯỢNG: NAUDIO (KIỂM SOÁT PHẦN CỨNG)
        // ==========================================
        private WaveInEvent _waveIn;
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _waveProvider;

        public event Action<string> OnIceCandidateReady;
        public event Action<string> OnOfferReady;
        public event Action<string> OnAnswerReady;
        public event Action<string> OnCallStateChanged;

        private List<RTCIceCandidateInit> _iceCandidateQueue = new List<RTCIceCandidateInit>();
        private bool _isRemoteDescriptionSet = false;

        public async Task InitializeCallAsync()
        {
            _isRemoteDescriptionSet = false;

            var config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = "stun:stun.l.google.com:19302" } }
            };

            _peerConnection = new RTCPeerConnection(config);

            // ==========================================
            // 🚀 KHỞI TẠO NAUDIO (ÉP WINDOWS PHÁT TIẾNG)
            // ==========================================
            try
            {
                // 1. Khởi tạo Loa (Chuẩn 8kHz 16-bit)
                _waveOut = new WaveOutEvent();
                _waveProvider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1))
                {
                    DiscardOnBufferOverflow = true // Chống nghẽn âm thanh
                };
                _waveOut.Init(_waveProvider);

                // 2. Khởi tạo Mic (Chuẩn 8kHz 16-bit)
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(8000, 16, 1),
                    BufferMilliseconds = 20 // Cắt luồng mic đúng 20ms cho mỗi gói tin
                };

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [LỖI NAUDIO] Không thể gọi phần cứng: {ex.Message}");
            }

            // ==========================================
            // 🚀 BÁO CHO WEBRTC BIẾT CHỈ DÙNG PCMU
            // ==========================================
            var pcmuFormat = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU);
            var audioTrack = new MediaStreamTrack(new List<AudioFormat> { pcmuFormat }, MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            _peerConnection.OnAudioFormatsNegotiated += (formats) =>
            {
                System.Diagnostics.Debug.WriteLine($"🎧 [WEBRTC] Đàm phán xong mạng! Tự quản lý Audio bằng NAudio.");
            };

            // ==========================================
            // 🎤 BẮT MIC BẰNG NAUDIO -> MÃ HÓA PCMU -> GỬI ĐI
            // ==========================================
            _waveIn.DataAvailable += (s, e) =>
            {
                // Chuyển âm thanh thô (PCM) thành chuẩn mạng PCMU (MuLaw)
                byte[] encoded = new byte[e.BytesRecorded / 2];
                int outIndex = 0;
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i);
                    encoded[outIndex++] = MuLawEncoder.LinearToMuLawSample(sample);
                }

                System.Diagnostics.Debug.WriteLine($"🎤 [NAUDIO MIC] Gửi {encoded.Length} bytes đi!");
                _peerConnection?.SendAudio(20, encoded);
            };

            // ==========================================
            // 🔊 NHẬN TỪ MẠNG -> GIẢI MÃ PCMU -> BƠM VÀO LOA NAUDIO
            // ==========================================
            _peerConnection.OnRtpPacketReceived += (IPEndPoint rep, SDPMediaTypesEnum media, RTPPacket rtpPkt) =>
            {
                if (media == SDPMediaTypesEnum.audio)
                {
                    byte[] payload = rtpPkt.Payload;

                    // Giải mã từ chuẩn mạng PCMU (MuLaw) về âm thanh thô (PCM)
                    byte[] decoded = new byte[payload.Length * 2];
                    int outIndex = 0;
                    for (int i = 0; i < payload.Length; i++)
                    {
                        short sample = MuLawDecoder.MuLawToLinearSample(payload[i]);
                        byte[] sampleBytes = BitConverter.GetBytes(sample);
                        decoded[outIndex++] = sampleBytes[0];
                        decoded[outIndex++] = sampleBytes[1];
                    }

                    // Bơm trực tiếp vào màng loa (Không qua SIPSorcery)
                    _waveProvider?.AddSamples(decoded, 0, decoded.Length);
                    System.Diagnostics.Debug.WriteLine($"🔊 [NAUDIO LOA] Bơm {decoded.Length} bytes vào màng loa!");
                }
            };

            // ==========================================
            // LỌC MẠNG TAILSCALE
            // ==========================================
            var validIps = GetValidLocalIPs();
            _peerConnection.onicecandidate += (candidate) =>
            {
                string candStr = candidate.candidate;
                bool isStunOrTurn = candStr.Contains("typ srflx") || candStr.Contains("typ relay");
                bool isLocalValid = validIps.Any(ip => candStr.Contains($" {ip} "));

                if (isLocalValid || isStunOrTurn)
                {
                    OnIceCandidateReady?.Invoke(candidate.toJSON());
                }
            };

            _peerConnection.onconnectionstatechange += (state) =>
            {
                Console.WriteLine($"Trạng thái WebRTC: {state}");
                OnCallStateChanged?.Invoke(state.ToString());
            };

            try
            {
                // BẬT MIC VÀ LOA
                _waveOut?.Play();
                _waveIn?.StartRecording();
                System.Diagnostics.Debug.WriteLine("✅ [NAUDIO] Khởi động Micro/Loa độc lập thành công!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [LỖI NGHIÊM TRỌNG] Không bật được Micro/Loa: {ex.Message}");
            }
        }

        private List<string> GetValidLocalIPs()
        {
            List<string> validIps = new List<string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                string desc = ni.Description.ToLower();
                string name = ni.Name.ToLower();

                if (desc.Contains("wsl") || name.Contains("wsl") ||
                    desc.Contains("vmware") || name.Contains("vmware") ||
                    desc.Contains("virtualbox") || desc.Contains("hyper-v"))
                {
                    continue;
                }

                foreach (var ipProps in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ipProps.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        validIps.Add(ipProps.Address.ToString());
                    }
                }
            }
            return validIps;
        }

        public async Task CreateOfferAsync()
        {
            var offer = _peerConnection.createOffer();
            await _peerConnection.setLocalDescription(offer);
            OnOfferReady?.Invoke(offer.toJSON());
        }

        public async Task ReceiveOfferAndCreateAnswerAsync(string offerJson)
        {
            if (RTCSessionDescriptionInit.TryParse(offerJson, out var offer))
            {
                _peerConnection.setRemoteDescription(offer);
                _isRemoteDescriptionSet = true;

                foreach (var c in _iceCandidateQueue) _peerConnection.addIceCandidate(c);
                _iceCandidateQueue.Clear();

                var answer = _peerConnection.createAnswer();
                await _peerConnection.setLocalDescription(answer);
                OnAnswerReady?.Invoke(answer.toJSON());
            }
        }

        public void ReceiveAnswer(string answerJson)
        {
            if (RTCSessionDescriptionInit.TryParse(answerJson, out var answer))
            {
                _peerConnection.setRemoteDescription(answer);
                _isRemoteDescriptionSet = true;

                foreach (var c in _iceCandidateQueue) _peerConnection.addIceCandidate(c);
                _iceCandidateQueue.Clear();
            }
        }

        public void AddIceCandidate(string candidateJson)
        {
            if (RTCIceCandidateInit.TryParse(candidateJson, out var candidate))
            {
                if (_peerConnection != null && _isRemoteDescriptionSet)
                {
                    _peerConnection.addIceCandidate(candidate);
                }
                else
                {
                    _iceCandidateQueue.Add(candidate);
                }
            }
        }

        public async Task CloseCallAsync()
        {
            _iceCandidateQueue.Clear();
            _isRemoteDescriptionSet = false;

            // TẮT NAUDIO SẠCH SẼ CHỐNG CRASH
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_peerConnection != null)
            {
                _peerConnection.Close("Call ended");
                _peerConnection = null;
            }
            await Task.CompletedTask;
        }
    }
}