# Đánh giá hiện trạng Project Hermes Chat App (Zero-Knowledge Architecture)

Dựa trên Context.md và kiến trúc 3-tier mong muốn, dưới đây là tình trạng hiện tại của dự án:

## 1. Tổng quan dự án (Đã đạt được)
- **Cấu trúc Solution chuẩn**: Đã chia thành 3 project là `Hermes.Server` (ASP.NET Core Web API), `Hermes.Client` (WPF app), và `Hermes.Shared` (Class Library). Refactoring file qua các class đã xong cơ bản.
- **SignalR Config**: Đã cài đặt NuGet và map `/chathub` thông qua `ChatHub.cs`.
- **MySql Database**: Khởi tạo schema hoàn chỉnh, chuẩn hóa 7 bảng như CONTEXT.
- **Crypto & E2EE**: Đã thiết lập `CryptoService.cs` theo chuẩn Zero-Knowledge: 
  - Derive Master Key (PBKDF2 -> AES-256).
  - Wrap/Unwrap RSA Private Key.
  - Mã hóa tin nhắn qua AES-GCM (Dùng Nonce + Tag). 

## 2. Những vi phạm (Violation) nghiêm trọng cần sửa khẩn cấp
Theo như quy tắc STRICT trong `CONTEXT.md`:
> **"IMPORTANT: This database is ONLY accessed by Hermes.Server. Client MUST NEVER connect directly to the MySQL Database. All data fetching/saving must go through HTTP Requests or SignalR."**

- **Tình trạng hiện tại**: `Hermes` (Client WPF) đang chứa các service như `AuthService.cs` và `ConversationService.cs`, trong đó gọi TRỰC TIẾP đến MySQL (`new MySqlConnection(...)` và *Dapper*). 
- **Cách khắc phục**:
  - Gỡ bỏ hoàn toàn gói `MySqlConnector` và chuỗi kết nối (Connection String) khỏi Project WPF (`Hermes`).
  - Xây dựng **Controllers** phía `Hermes.Server` (hoặc method ở SignalR Hub) để tiếp nhận lệnh đăng ký, Load thông tin Username, tạo Conversation.
  - Phía WPF Client (`Hermes`), chỉ giao tiếp với Server qua `HttpClient` (Rest API) hoặc `HubConnection` (SignalR).

## 3. Tính năng còn thiếu (Missing features)
1. **API Endpoints ở Server**: 
   - Hiện Server chỉ có mỗi `ChatHub.cs`. Chưa có API cho Authentication (Thêm User, lấy thông tin User), tạo Chat/Group, đánh dấu đã đọc (IsRead), v.v.
2. **Cơ chế Burn-on-Read (TimeToLive)**:
   - Logic Background Service phía Server dọn dẹp tin nhắn (CronJob).
   - Dispatcher Timer phía Client cho Cửa sổ Chat.
3. **Đồng bộ Key Exchange khi tạo Chat**:
   - Khi nhắn tin, user cần lấy được `PublicKey` của đối phương (thông qua API) để mã hóa Session Key. Phía Server chưa có API cung cấp Public Key của Contact/Conversation.
4. **Logic Voice Call**:
   - `VoiceService.cs` đã dựng form UDP Hole Punching nhưng đang bị *comment out* thư viện `NAudio`. Cần tiếp tục hoàn thiện vòng lặp stream audio và xử lý Start/Stop ghi âm.

## KẾT LUẬN CO-PILOT
Để dự án tuân thủ đúng yêu cầu đề ra (`Context.md`), Bước tiếp theo TIÊN QUYẾT là:
Di dời toàn bộ logic gọi Database (`Dapper/MySqlConnector`) sang Web API `Hermes.Server` và viết lớp `HttpClient` cho WPF để gọi nó.