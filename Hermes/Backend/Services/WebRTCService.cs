using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Hermes.Client.Services
{
    public class WebRTCService
    {
        private RTCPeerConnection _peerConnection;
        private WindowsAudioEndPoint _audioEndPoint;

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
            // 🚀 BƯỚC 1: DÙNG WASAPI THAY VÌ WAVEOUT CỦA WINDOWS (CÚ CHỐT)
            // ==========================================
            // Chữ 'true' ở cuối cực kỳ quan trọng, nó ép Windows 11 phải cho phép phát âm thanh!
            _audioEndPoint = new WindowsAudioEndPoint(new AudioEncoder(), -1, -1, true);

            // ==========================================
            // 🚀 BƯỚC 2: CODEC G722 & ĐỒNG BỘ TUYỆT ĐỐI MIC VÀ LOA
            // ==========================================
            var allFormats = _audioEndPoint.GetAudioSourceFormats();
            var targetFormat = allFormats.Where(f => f.FormatName.ToUpper() == "G722").ToList();
            if (targetFormat.Count == 0) targetFormat = allFormats.Where(f => f.FormatName.ToUpper() == "PCMU").ToList();

            var selectedFormat = targetFormat.First();

            var audioTrack = new MediaStreamTrack(targetFormat, MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            // Ép phần cứng: Mic thu 16kHz, Loa phát 16kHz (Bluetooth mới không bị ngáo)
            _audioEndPoint.SetAudioSourceFormat(selectedFormat);
            _audioEndPoint.SetAudioSinkFormat(selectedFormat);

            // Xử lý đàm phán an toàn tuyệt đối (Đã fix lỗi dấu ??)
            _peerConnection.OnAudioFormatsNegotiated += (formats) =>
            {
                var g722List = formats.Where(f => f.FormatName.ToUpper() == "G722").ToList();
                var finalFormat = g722List.Count > 0 ? g722List.First() : formats.First();
                _audioEndPoint.SetAudioSourceFormat(finalFormat);
                _audioEndPoint.SetAudioSinkFormat(finalFormat);
            };

            // ==========================================
            // 🚀 BƯỚC 3: TRẠM KIỂM ĐỊNH LÕI GIẢI MÃ
            // ==========================================
            // Hàm này chỉ chạy khi bộ giải mã SIPSorcery đã dịch thành công gói tin 80 bytes thành âm thanh thực

            System.Diagnostics.Debug.WriteLine($"🎧 [CODEC] Đã chốt chuẩn đồng bộ: {selectedFormat.FormatName} (WASAPI)");

            // ==========================================
            // THU MIC VÀ GỬI (ĐÃ CHỐNG CRASH)
            // ==========================================
            _audioEndPoint.OnAudioSourceEncodedSample += (duration, payload) =>
            {
                bool isSilence = payload.All(b => b == 0 || b == 255);
                if (!isSilence)
                {
                    System.Diagnostics.Debug.WriteLine($"🎤 [MIC ĐANG SỐNG] Gửi {payload.Length} bytes âm thanh THẬT đi!");
                }

                // Dùng ?. để nếu cúp máy ngang, _peerConnection bị null thì không văng lỗi
                _peerConnection?.SendAudio(duration, payload);
            };

            // ==========================================
            // NHẬN TỪ MẠNG VÀ PHÁT LOA (GỘP LÀM 1 LẦN DUY NHẤT)
            // ==========================================
            _peerConnection.OnRtpPacketReceived += (System.Net.IPEndPoint rep, SDPMediaTypesEnum media, RTPPacket rtpPkt) =>
            {
                if (media == SDPMediaTypesEnum.audio)
                {
                    System.Diagnostics.Debug.WriteLine($"🔊 [MÁY NHẬN] Nhận {rtpPkt.Payload.Length} bytes | PayloadType: {rtpPkt.Header.PayloadType}");

                    // Dùng ?. chống crash
                    _audioEndPoint?.GotAudioRtp(
                        rep, rtpPkt.Header.SyncSource, rtpPkt.Header.SequenceNumber,
                        rtpPkt.Header.Timestamp, rtpPkt.Header.PayloadType,
                        rtpPkt.Header.MarkerBit == 1, rtpPkt.Payload);
                }
            };

            // ==========================================
            // BỘ LỌC MẠNG TAILSCALE
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
                await _audioEndPoint.StartAudio();
                System.Diagnostics.Debug.WriteLine("✅ [TEST] Khởi động Micro/Loa thành công!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [LỖI NGHIÊM TRỌNG] Không thể bật Micro/Loa: {ex.Message}");
            }
        }

        private List<string> GetValidLocalIPs()
        {
            List<string> validIps = new List<string>();

            // Quét toàn bộ card mạng trên máy tính
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Bỏ qua các card đang tắt hoặc card Loopback (127.0.0.1)
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                string desc = ni.Description.ToLower();
                string name = ni.Name.ToLower();

                // LỌC THÔNG MINH: Bỏ qua các mạng sinh ra do máy ảo, giữ lại Tailscale và mạng thật
                if (desc.Contains("wsl") || name.Contains("wsl") ||
                    desc.Contains("vmware") || name.Contains("vmware") ||
                    desc.Contains("virtualbox") || desc.Contains("hyper-v"))
                {
                    continue;
                }

                // Lấy các IP của card mạng hợp lệ
                foreach (var ipProps in ni.GetIPProperties().UnicastAddresses)
                {
                    // Ưu tiên lấy IPv4 (Tailscale và WiFi/LAN dùng IPv4 là ổn định nhất cho WebRTC P2P)
                    if (ipProps.Address.AddressFamily == AddressFamily.InterNetwork)
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

                // Bơm toàn bộ IP đang xếp hàng chờ vào
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

            if (_audioEndPoint != null)
            {
                await _audioEndPoint.CloseAudio();
                _audioEndPoint = null;
            }

            if (_peerConnection != null)
            {
                _peerConnection.Close("Call ended");
                _peerConnection = null;
            }
        }
    }
}