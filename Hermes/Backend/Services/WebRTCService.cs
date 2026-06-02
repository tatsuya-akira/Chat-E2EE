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
            // KHÔNG clear queue ở đây! Giữ nguyên các IP dự phòng.

            var config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = "stun:stun.l.google.com:19302" } }
            };

            _peerConnection = new RTCPeerConnection(config);

            _audioEndPoint = new WindowsAudioEndPoint(new AudioEncoder());
            var audioTrack = new MediaStreamTrack(_audioEndPoint.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            // ==========================================
            // FIX CODEC: CHỐT CHUẨN ÂM THANH NGAY TỪ ĐẦU
            // Ưu tiên OPUS (rất mượt), nếu không có thì xuống PCMU, bí quá thì lấy cái đầu tiên
            // ==========================================
            var formats = _audioEndPoint.GetAudioSourceFormats();
            var fallbackFormat = formats.First();

            var opusFormats = formats.Where(f => f.FormatName.ToUpper() == "OPUS").ToList();
            var pcmuFormats = formats.Where(f => f.FormatName.ToUpper() == "PCMU").ToList();

            if (opusFormats.Count > 0) fallbackFormat = opusFormats.First();
            else if (pcmuFormats.Count > 0) fallbackFormat = pcmuFormats.First();

            _audioEndPoint.SetAudioSinkFormat(fallbackFormat);
            Debug.WriteLine($"🎧 [FIXED] Đã ép Loa dùng chuẩn: {fallbackFormat.FormatName}");

            // ==========================================
            // THU ÂM TỪ MIC VÀ PHÁT QUA MẠNG
            // ==========================================
            _audioEndPoint.OnAudioSourceEncodedSample += (duration, payload) =>
            {
                bool isSilence = payload.All(b => b == 0 || b == 255);
                if (!isSilence)
                {
                    // Tạm ẩn log để đỡ giật màn hình, nếu muốn dò mic thì mở lên
                    Debug.WriteLine($"🎤 [MIC ĐANG SỐNG] Gửi {payload.Length} bytes âm thanh THẬT đi!");
                }
                else
                {
                    Debug.WriteLine("⚠️ [CẢNH BÁO] Mic đang gửi đi sự im lặng tuyệt đối (Lỗi Mic hoặc bị Windows chặn)!");
                }

                _peerConnection.SendAudio(duration, payload);
            };

            // ==========================================
            // NHẬN ÂM THANH TỪ MẠNG ĐỔ RA LOA
            // ==========================================
            _peerConnection.OnRtpPacketReceived += (System.Net.IPEndPoint rep, SDPMediaTypesEnum media, RTPPacket rtpPkt) =>
            {
                if (media == SDPMediaTypesEnum.audio)
                {
                    // Tạm ẩn log
                    // Debug.WriteLine("🔊 [TEST] Đã nhận được âm thanh từ mạng!"); 
                    _audioEndPoint.GotAudioRtp(
                        rep, rtpPkt.Header.SyncSource, rtpPkt.Header.SequenceNumber,
                        rtpPkt.Header.Timestamp, rtpPkt.Header.PayloadType,
                        rtpPkt.Header.MarkerBit == 1, rtpPkt.Payload);
                }
            };

            // 1. Lấy danh sách IP sạch (động) ngay khi khởi tạo cuộc gọi
            var validIps = GetValidLocalIPs();

            _peerConnection.onicecandidate += (candidate) =>
            {
                string candStr = candidate.candidate;

                // 2. Candidate từ STUN server (srflx) hoặc TURN (relay) luôn là IP Public xịn -> Luôn cho phép
                bool isStunOrTurn = candStr.Contains("typ srflx") || candStr.Contains("typ relay");

                // 3. Kiểm tra xem IP trong chuỗi Candidate có nằm trong danh sách IP Sạch không
                // Thêm khoảng trắng 2 đầu để match chính xác (vd: " 100.105.164.111 ")
                bool isLocalValid = validIps.Any(ip => candStr.Contains($" {ip} "));

                if (isLocalValid || isStunOrTurn)
                {
                    OnIceCandidateReady?.Invoke(candidate.toJSON());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 [DYNAMIC FILTER] Đã chặn IP ảo/rác: {candStr}");
                }
            };
            // XÓA OnAudioFormatsNegotiated ĐỂ TRÁNH LỖI XUNG ĐỘT GHI ĐÈ

            _peerConnection.onconnectionstatechange += (state) =>
            {
                Console.WriteLine($"Trạng thái WebRTC: {state}");
                OnCallStateChanged?.Invoke(state.ToString());
            };

            try
            {
                await _audioEndPoint.StartAudio();
                Debug.WriteLine("✅ [TEST] Khởi động Micro/Loa thành công!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [LỖI NGHIÊM TRỌNG] Không thể bật Micro/Loa: {ex.Message}");
            }
            _peerConnection.OnRtpPacketReceived += (System.Net.IPEndPoint rep, SDPMediaTypesEnum media, RTPPacket rtpPkt) =>
            {
                if (media == SDPMediaTypesEnum.audio)
                {
                    // 1. In ra số byte nhận được để so khớp với bên gửi
                    System.Diagnostics.Debug.WriteLine($"🔊 [MÁY NHẬN] Bắt được {rtpPkt.Payload.Length} bytes từ IP {rep.Address}");

                    // 2. Hàm này chính là hàm NÉM dữ liệu vào bộ giải mã (Decoder) và phát ra loa
                    _audioEndPoint.GotAudioRtp(
                        rep, rtpPkt.Header.SyncSource, rtpPkt.Header.SequenceNumber,
                        rtpPkt.Header.Timestamp, rtpPkt.Header.PayloadType,
                        rtpPkt.Header.MarkerBit == 1, rtpPkt.Payload);
                }
            };
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