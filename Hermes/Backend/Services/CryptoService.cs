using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Hermes.Backend.Services
{
    public static class CryptoService
    {
        // Đường dẫn lưu file khóa bảo mật trong thư mục AppData của User Windows
        private static readonly string KeyFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HermesChat",
            "user_secure.dat"
        );

        // 1. MÃ HÓA VÀ LƯU PRIVATE KEY XUỐNG Ổ CỨNG
        public static void SavePrivateKeyLocal(string privateKeyRaw)
        {
            try
            {
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(privateKeyRaw);

                // DPAPI mã hóa: Khóa chặt vào tài khoản Windows hiện tại
                byte[] encryptedBytes = ProtectedData.Protect(
                    plaintextBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                // Tạo thư mục nếu chưa tồn tại
                string directory = Path.GetDirectoryName(KeyFilePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                // Ghi file nhị phân xuống ổ cứng
                File.WriteAllBytes(KeyFilePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi DPAPI khi lưu khóa: {ex.Message}");
            }
        }

        // 2. GIẢI MÃ VÀ TẢI PRIVATE KEY TỪ Ổ CỨNG LÊN RAM
        public static string LoadPrivateKeyLocal()
        {
            try
            {
                if (!File.Exists(KeyFilePath)) return null;

                byte[] encryptedBytes = File.ReadAllBytes(KeyFilePath);

                // DPAPI giải mã: Nếu copy file này sang máy khác, hàm này sẽ ném ra ngoại lệ lập tức
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException)
            {
                // File bị phá hoại hoặc bị mang sang máy khác trái phép
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // 3. XÓA KHÓA CỤC BỘ (Khi người dùng bấm Đăng xuất)
        public static void ClearPrivateKeyLocal()
        {
            if (File.Exists(KeyFilePath))
            {
                File.Delete(KeyFilePath);
            }
        }
        private const int Iterations = 300_000;
        private const int KeySize = 32; // 256 bits

        public static string GenerateSalt()
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return Convert.ToBase64String(salt);
        }

        public static byte[] DeriveMasterKey(string password, string saltBase64)
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(KeySize);
            }
        }

        public static (string PublicKeyBase64, string PrivateKeyBase64) GenerateRSAKeys()
        {
            using (var rsa = RSA.Create(2048))
            {
                byte[] pub = rsa.ExportRSAPublicKey();
                byte[] priv = rsa.ExportRSAPrivateKey();
                return (Convert.ToBase64String(pub), Convert.ToBase64String(priv));
            }
        }

        // --- CHUẨN HÓA MÃ HÓA LAI AES-GCM (HYBRID ENCRYPTION) ---

        public static string EncryptWithAES(string plainText, byte[] key)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] nonce = new byte[12]; // GCM kích thước Nonce tiêu chuẩn

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            byte[] cipherText = new byte[plainBytes.Length];
            byte[] tag = new byte[16]; // GCM kích thước Auth Tag tiêu chuẩn

            using (var aesGcm = new AesGcm(key))
            {
                aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
            }

            // Gộp chuỗi theo cấu trúc: [Nonce (12B)] + [Tag (16B)] + [CipherText]
            byte[] result = new byte[nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, result, nonce.Length + tag.Length, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        public static string DecryptWithAES(string cipherTextBase64, byte[] key)
        {
            if (string.IsNullOrEmpty(cipherTextBase64)) return string.Empty;

            try
            {
                byte[] data = Convert.FromBase64String(cipherTextBase64);

                int nonceSize = 12;
                int tagSize = 16;

                // Kiểm tra gói tin có đủ độ dài tối thiểu của cấu trúc GCM hay không
                if (data.Length < nonceSize + tagSize)
                    return "[Lỗi: Gói tin mã hóa không hợp lệ hoặc bị korrupt]";

                byte[] nonce = new byte[nonceSize];
                byte[] tag = new byte[tagSize];
                byte[] cipherText = new byte[data.Length - nonceSize - tagSize];

                // Bóc tách chính xác từng phân đoạn từ mảng byte tổng thể
                Buffer.BlockCopy(data, 0, nonce, 0, nonceSize);
                Buffer.BlockCopy(data, nonceSize, tag, 0, tagSize);
                Buffer.BlockCopy(data, nonceSize + tagSize, cipherText, 0, cipherText.Length);

                byte[] plainBytes = new byte[cipherText.Length];

                using (var aesGcm = new AesGcm(key))
                {
                    // Tự động kiểm tra tính toàn vẹn thông qua Auth Tag
                    aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);
                }

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                // Trả về thông báo lỗi chung nếu chuỗi cipher bị sửa đổi trái phép (Auth Tag sai)
                return "[Lỗi: Không thể xác thực hoặc giải mã tin nhắn này]";
            }
            catch (Exception)
            {
                return "[Lỗi hệ thống khi xử lý giải mã E2EE]";
            }
        }



        public static string EncryptPrivateKey(string privateKeyBase64, byte[] masterKey)
        {
            return EncryptWithAES(privateKeyBase64, masterKey);
        }

        public static string DecryptPrivateKey(string wrappedPrivateKeyBase64, byte[] masterKey)
        {
            return DecryptWithAES(wrappedPrivateKeyBase64, masterKey);
        }

        // For E2EE Session Key Distribution
        public static string EncryptSessionKeyWithRSA(byte[] sessionKey, string publicKeyBase64)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
                byte[] encrypted = rsa.Encrypt(sessionKey, RSAEncryptionPadding.OaepSHA256);
                return Convert.ToBase64String(encrypted);
            }
        }

        public static byte[] DecryptSessionKeyWithRSA(string encryptedSessionKeyBase64, string privateKeyBase64)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
                byte[] decrypted = rsa.Decrypt(Convert.FromBase64String(encryptedSessionKeyBase64), RSAEncryptionPadding.OaepSHA256);
                return decrypted;
            }
        }
        public static byte[] GenerateRandomKey(int size = 32)
        {
            byte[] key = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            return key;
        }
    }
}
