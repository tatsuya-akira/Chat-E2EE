# Hermes Chat App

Một ứng dụng nhắn tin bảo mật trên Desktop (WPF) với trọng tâm là tính năng **Mã hóa đầu cuối (End-to-End Encryption - E2EE)** và **Mật khẩu giả (Duress Password)**.

## Giới thiệu
Hermes Chat App được xây dựng với mục tiêu cung cấp một nền tảng giao tiếp an toàn, bảo vệ dữ liệu người dùng ngay từ thiết bị cá nhân. Dự án áp dụng các chuẩn mật mã học hiện đại như RSA, AES-GCM và cơ chế Key Wrapping (PBKDF2) để đảm bảo không ai - kể cả máy chủ - có thể đọc được nội dung tin nhắn.

## Các tính năng nổi bật

### 1. Giao diện & Trải nghiệm
- **Cơ sở hạ tầng Auth:** Đăng nhập, Đăng ký, Quên mật khẩu qua Firebase Auth.
- **Bảo toàn dữ liệu:** Tự động Rollback (xóa user Firebase) nếu lưu trữ xuống MySQL thất bại.
- **UI/UX (WPF MVVM):** Giao diện Chat trực quan với hiệu ứng bong bóng tin nhắn (Chat bubbles), phân biệt lề trái/phải.
- **Quản lý danh bạ:** Khởi tạo chat cá nhân và chat nhóm.
- **Bảo mật cục bộ:** Quản lý cấu hình qua biến môi trường (`.env`), tránh lộ API Key.

### 2. Bảo mật & Mật mã học
- **Mã hóa Hybrid (E2EE):** Kết hợp RSA (2048/4096-bit) để trao đổi khóa và AES-GCM để mã hóa nội dung tin nhắn thời gian thực.
- **Key Wrapping:** Bảo vệ Private Key của người dùng bằng cách mã hóa nó với Khóa Master (dẫn xuất từ Mật khẩu + Salt qua hàm PBKDF2) trước khi lưu lên MySQL.
- **Mật khẩu ngụy trang (Duress Password):** Hỗ trợ một mật khẩu phụ. Khi bị ép buộc mở ứng dụng, việc nhập mật khẩu phụ sẽ mở ra một "không gian giả" với danh bạ và tin nhắn mồi, bảo vệ dữ liệu thật.

## Công nghệ sử dụng
- **Front-end:** C#, WPF, MVVM Pattern.
- **Back-end:** ASP.NET Core SignalR (WebSockets).
- **Database:** MySQL, Firebase Authentication.
- **Cryptography:** `System.Security.Cryptography` (RSA, AesGcm, Rfc2898DeriveBytes).

## Hướng dẫn cài đặt (Local Development)
1. Clone repository về máy.
2. Tạo file `.env` tại thư mục gốc của project và điền các thông tin:
   ```env
   FIREBASE_API_KEY=your_api_key
   FIREBASE_AUTH_DOMAIN=your_project.firebaseapp.com
   MYSQL_CONNECTION_STRING=Server=localhost;Database=hermes_db;Uid=root;Pwd=yourpassword;
```
## Timeline
https://docs.google.com/spreadsheets/d/1RXGLQFEikIs9vv7t25ES2CM7M_rmdpR-917V4to-uk0/edit?usp=sharing
