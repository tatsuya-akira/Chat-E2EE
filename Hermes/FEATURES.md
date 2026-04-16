# Danh sách các tính năng đã phát triển (Hermes Chat App)

## 1. Giao diện người dùng (WPF)
- **Cửa sổ Đăng nhập (`MainWindow.xaml`)**: Giao diện đăng nhập cơ bản với Email và Mật khẩu.
- **Cửa sổ Đăng ký (`RegisterWindow.xaml`)**: Form đăng ký tài khoản với các trường: Tên hiển thị (Username), Email, Mật khẩu, và Xác nhận mật khẩu.
- **Cửa sổ Quên mật khẩu (`ForgotPasswordWindow.xaml`)**: Giao diện yêu cầu cấp lại mật khẩu thông qua Email.
- **Cửa sổ Chat (`ChatWindow.xaml`)**: Giao diện chính của ứng dụng chat, hiển thị danh sách bạn bè và nội dung tin nhắn.

## 2. Xác thực người dùng (Firebase Authentication)
- **Đăng ký tài khoản (Sign Up)**: Tạo tài khoản mới bằng Email và Mật khẩu qua Firebase Auth.
- **Đăng nhập (Sign In)**: Đăng nhập vào hệ thống bằng tài khoản đã tạo.
- **Khôi phục mật khẩu**: Gửi email chứa liên kết khôi phục mật khẩu (Password Reset) cho người dùng.
- **Bắt lỗi xác thực**: Tùy chỉnh thông báo lỗi tiếng Việt khi nhập sai mật khẩu, email đã tồn tại, hoặc tài khoản chưa được đăng ký.

## 3. Quản lý Cơ sở dữ liệu (MySQL)
- **Kết nối CSDL**: Sử dụng `MySql` để kết nối với cơ sở dữ liệu `hermes_db`.
- **Thiết kế CSDL (`hermes_db.sql`)**:
  - Bảng `Users` lưu trữ `Id` (Firebase UID) và `Email`.
  - Bảng `Info` lưu trữ `FullName` (Tên hiển thị), `AvatarUrl` và `StatusMessage`, có liên kết khóa ngoại (Foreign Key) với bảng `Users`.
  - Bổ sung ràng buộc `UNIQUE` (`UQ_FullName`) cho `FullName` trên bảng `Info` để bảo đảm mỗi Username là khác biệt.
- **Lưu thông tin khi đăng ký**: Tự động chèn dữ liệu vào bảng `Users` và `Info` ngay sau khi đăng ký Firebase thành công.

## 4. Xử lý Logic & Bảo mật
- **Kiểm tra đầu vào (Validation)**:
  - Bắt lỗi bỏ trống các trường dữ liệu.
  - Sử dụng Regex để xác thực định dạng Email.
  - Ràng buộc mật khẩu từ 6 đến 20 ký tự.
  - So sánh khớp Mật khẩu và Xác nhận mật khẩu.
- **Bảo toàn dữ liệu (Rollback Data)**: Khi đăng ký, nếu việc lưu thông tin người dùng vào MySQL thất bại, hệ thống sẽ **tự động xóa tài khoản Firebase** vừa được tạo để tránh tình trạng bất đồng bộ dữ liệu giữa Firebase và MySQL.
- **Khóa nút bấm (Disable Button)**: Tạm thời vô hiệu hóa các nút (Đăng nhập, Đăng ký, Gửi mã) trong lúc chờ phản hồi từ Server (API) để tránh spam click.
- **Cấu hình Biến môi trường (.env)**:
  - Sử dụng `DotNetEnv` để nạp các thông tin nhạy cảm như Firebase ApiKey, AuthDomain hoặc MySQL Connection String.
  - Ẩn file môi trường qua `.gitignore` trước khi commit lên repository.

## 5. Quản lý Trò chuyện & Cài đặt (Nâng cao)
- **Giao diện Chat chi tiết (`ChatWindow.xaml`)**:
  - Binding cấu trúc dữ liệu (`ChatModel`, `MessageModel`) để hiển thị động (MVVM - ObservableCollection).
  - Phân biệt tự động tin nhắn của "Bản thân" (lề phải, màu xanh) và "Người gửi khác" (lề trái) thông qua `BooleanToVisibilityInverseConverter`.
- **Chức năng Tạo trò chuyện mới (`CreateChatWindow.xaml`)**:
  - Cung cấp tùy chọn tạo **Chat cá nhân** hoặc **Chat nhóm**.
  - Kiểm tra đối tượng hợp lệ trong database thông qua "Tên hiển thị" hoặc "Email".
  - Nếu là Group (Nhóm), hệ thống bắt buộc nhập Tên nhóm và tối thiểu 2 người tham gia khác hợp lệ.
- **Cài đặt & Tài khoản (`SettingsWindow.xaml`)**:
  - Xem thông tin tài khoản hiện tại (UID và Email).
  - Chức năng đăng xuất: Xóa phiên (Session) người dùng cục bộ, tự động đóng các cửa sổ hộp thoại đang mở và chuyển hướng về giao diện đăng nhập gốc.
