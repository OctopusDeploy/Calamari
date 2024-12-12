using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Calamari.Common.Plumbing.Extensions
{
    public class AesEncryption
    {
        const int KeySizeBits = 256;
        const int KeySizeBytes = KeySizeBits / 8;

        const int BlockSizeBits = 128;
        const int BlockSizeBytes = KeySizeBits / 8;

        const int PasswordSaltIterations = 1000;
        public const string SaltRaw = "Octopuss";
        static readonly byte[] PasswordPaddingSalt = Encoding.UTF8.GetBytes(SaltRaw);
        static readonly byte[] IvPrefix = Encoding.UTF8.GetBytes("IV__");

        static readonly Random RandomGenerator = new Random();

        readonly byte[] key;

        public AesEncryption(string password)
        {
            key = GetEncryptionKey(password);
        }

        public string Decrypt(byte[] encrypted)
        {
            byte[] iv;
            var aesBytes = ExtractIV(encrypted, out iv);
            using (var algorithm = GetCryptoProvider(iv))
            using (var dec = algorithm.CreateDecryptor())
            using (var ms = new MemoryStream(aesBytes))
            using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
            using (var sr = new StreamReader(cs, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        public byte[] Encrypt(string plaintext)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plaintext);
            using (var algorithm = GetCryptoProvider())
            using (var cryptoTransform = algorithm.CreateEncryptor())
            using (var stream = new MemoryStream())
            {
                // The IV is randomly generated each time so safe to append
                stream.Write(IvPrefix, 0, IvPrefix.Length);
                stream.Write(algorithm.IV, 0, algorithm.IV.Length);
                using (var cs = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Write))
                {
                    cs.Write(plainTextBytes, 0, plainTextBytes.Length);
                }

                /*
                For testing purposes
                var key hex = BitConverter.ToString(algorithm.Key).Replace("-", string.Empty);
                var iv hex = BitConverter.ToString(algorithm.IV).Replace("-", string.Empty);
                var enc b64 = Convert.ToBase64String(stream.ToArray());
                */
                return stream.ToArray();
            }
        }

        Aes GetCryptoProvider(byte[]? iv = null)
        {
            var provider = new AesCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = KeySizeBits,
                BlockSize = BlockSizeBits,
                Key = key
            };
            if (iv != null)
                provider.IV = iv;
            
            return provider;
        }

        public static byte[] ExtractIV(byte[] encrypted, out byte[] iv)
        {
            var ivLength = BlockSizeBytes;
            iv = new byte[ivLength];
            Buffer.BlockCopy(encrypted,
                IvPrefix.Length,
                iv,
                0,
                ivLength);

            var ivDataLength = IvPrefix.Length + ivLength;
            var aesDataLength = encrypted.Length - ivDataLength;
            var aesData = new byte[aesDataLength];
            Buffer.BlockCopy(encrypted,
                ivDataLength,
                aesData,
                0,
                aesDataLength);
            return aesData;
        }

        public static byte[] GetEncryptionKey(string encryptionPassword)
        {
            var passwordGenerator = new Rfc2898DeriveBytes(encryptionPassword, PasswordPaddingSalt, PasswordSaltIterations);
            return passwordGenerator.GetBytes(KeySizeBytes);
        }

        public static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(
                Enumerable.Repeat(chars, length)
                    .Select(s => s[RandomGenerator.Next(s.Length)])
                    .ToArray());
        }
    }
}