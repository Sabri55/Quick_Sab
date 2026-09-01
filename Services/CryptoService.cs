using System;
using System.Security.Cryptography;
using System.Text;

namespace Quick_Sab.Services
{
    /// <summary>AES-256-CBC text encryption using the Key / IV stored in the configuration.</summary>
    public static class CryptoService
    {
        /// <summary>AES-256 key length, in characters (ASCII).</summary>
        public const int KeyLength = 32;

        /// <summary>AES block / IV length, in characters (ASCII).</summary>
        public const int IvLength = 16;

        /// <summary>Returns an error message when the key / IV pair is unusable, null when it is valid.</summary>
        public static string Validate(string key, string iv)
        {
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(iv))
                return "No AES key is configured.";
            if ((key ?? "").Length != KeyLength || Encoding.UTF8.GetByteCount(key ?? "") != KeyLength)
                return "The AES key must be exactly " + KeyLength + " characters (ASCII).";
            if ((iv ?? "").Length != IvLength || Encoding.UTF8.GetByteCount(iv ?? "") != IvLength)
                return "The AES IV must be exactly " + IvLength + " characters (ASCII).";
            return null;
        }

        /// <summary>Encrypts plain text and returns the cipher as Base64.</summary>
        public static string Encrypt(string plainText, string key, string iv)
        {
            using (var aes = Create(key, iv))
            using (var enc = aes.CreateEncryptor())
            {
                var data = Encoding.UTF8.GetBytes(plainText ?? "");
                var cipher = enc.TransformFinalBlock(data, 0, data.Length);
                return Convert.ToBase64String(cipher);
            }
        }

        /// <summary>Decrypts a Base64 cipher back to plain text.</summary>
        public static string Decrypt(string base64, string key, string iv)
        {
            using (var aes = Create(key, iv))
            using (var dec = aes.CreateDecryptor())
            {
                var data = Convert.FromBase64String((base64 ?? "").Trim());
                var plain = dec.TransformFinalBlock(data, 0, data.Length);
                return Encoding.UTF8.GetString(plain);
            }
        }

        private static Aes Create(string key, string iv)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes(iv);
            return aes;
        }
    }
}
