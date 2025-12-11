using System;
using System.Security.Cryptography;

namespace AuctionSite.Models
{
    public static class PasswordHelper
    {
        // パスワードをハッシュ化（PBKDF2 + Salt）
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password must not be empty.", nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(16); // ランダムソルト
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            // バージョン + ソルト + ハッシュ をまとめて保存
            byte[] result = new byte[1 + salt.Length + hash.Length];
            result[0] = 0x01; // バージョン
            Buffer.BlockCopy(salt, 0, result, 1, salt.Length);
            Buffer.BlockCopy(hash, 0, result, 1 + salt.Length, hash.Length);

            return Convert.ToBase64String(result);
        }

        // パスワード検証
        public static bool VerifyPassword(string password, string hashed)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(hashed);
            }
            catch
            {
                return false;
            }

            if (bytes.Length < 1 + 16 + 32 || bytes[0] != 0x01)
                return false;

            byte[] salt = new byte[16];
            Buffer.BlockCopy(bytes, 1, salt, 0, salt.Length);
            byte[] storedHash = new byte[32];
            Buffer.BlockCopy(bytes, 1 + salt.Length, storedHash, 0, storedHash.Length);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] newHash = pbkdf2.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(storedHash, newHash);
        }
    }
}
