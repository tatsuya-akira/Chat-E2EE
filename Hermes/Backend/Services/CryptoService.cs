using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Hermes.Backend.Services
{
    public static class CryptoService
    {
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

        public static string EncryptWithAES(string plainText, byte[] key)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] nonce = new byte[12]; // GCM standard nonce size
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            byte[] cipherText = new byte[plainBytes.Length];
            byte[] tag = new byte[16]; // auth tag

            using (var aesGcm = new AesGcm(key))
            {
                aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
            }

            // Return Base64(nonce + tag + cipherText)
            byte[] result = new byte[nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, result, nonce.Length + tag.Length, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        public static string DecryptWithAES(string cipherTextBase64, byte[] key)
        {
            byte[] data = Convert.FromBase64String(cipherTextBase64);
            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[data.Length - nonce.Length - tag.Length];

            Buffer.BlockCopy(data, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(data, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(data, nonce.Length + tag.Length, cipherText, 0, cipherText.Length);

            byte[] plainBytes = new byte[cipherText.Length];

            using (var aesGcm = new AesGcm(key))
            {
                aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
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
    }
}
