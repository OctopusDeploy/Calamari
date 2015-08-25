using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Calamari.Util
{
    public class ScriptVariableEncryptor
    {
        private byte[] passwordBytes;

        public ScriptVariableEncryptor(string passphrase)
        {
            passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        }

        private static byte[] RandomBytes(int size)
        {
            var array = new byte[size];
            new Random().NextBytes(array);
            return array;
        }

        public static string RandomString(int byteSize)
        {
            return Convert.ToBase64String(RandomBytes(byteSize));
        }

        public string Encrypt(string text)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(text);
            var salt = RandomBytes(8);

            using (var algorithm = GetAlgorithm(salt))
            using (var cryptoTransform = algorithm.CreateEncryptor())
            using (var stream = new MemoryStream())
            {
                using (var cs = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Write))
                {
                    cs.Write(plainTextBytes, 0, plainTextBytes.Length);
                }
                var encrypted = stream.ToArray();
                return Convert.ToBase64String(AppendSalt(encrypted, salt));
            }
        }

        private byte[] AppendSalt(byte[] encrypted, byte[] salt)
        {
            int resultLength = encrypted.Length + 8 + 8;
            byte[] saltPrefix = Encoding.UTF8.GetBytes("Salted__");
            byte[] result = new byte[resultLength];

            Buffer.BlockCopy(saltPrefix, 0, result, 0, saltPrefix.Length);
            Buffer.BlockCopy(salt, 0, result, 8, salt.Length);
            Buffer.BlockCopy(encrypted, 0, result, 16, encrypted.Length);
            return result;
        }

        private byte[] ExtractSalt(byte[] encrypted, out byte[] salt)
        {
            salt = new byte[8];
            Buffer.BlockCopy(encrypted, 8, salt, 0, 8);

            int aesDataLength = encrypted.Length - 16;
            var aesData = new byte[aesDataLength];
            Buffer.BlockCopy(encrypted, 16, aesData, 0, aesDataLength);
            return aesData;
        }

        private byte[] GetKey(byte[] salt, out byte[] iv)
        {
            byte[] key;
            using (var md5 = MD5.Create())
            {
                int preKeyLength = passwordBytes.Length + salt.Length;
                byte[] preKey = new byte[preKeyLength];
                Buffer.BlockCopy(passwordBytes, 0, preKey, 0, passwordBytes.Length);
                Buffer.BlockCopy(salt, 0, preKey, passwordBytes.Length, salt.Length);

                key = md5.ComputeHash(preKey);

                int preIVLength = key.Length + preKeyLength;
                byte[] preIV = new byte[preIVLength];

                Buffer.BlockCopy(key, 0, preIV, 0, key.Length);
                Buffer.BlockCopy(preKey, 0, preIV, key.Length, preKey.Length);
                iv = md5.ComputeHash(preIV);
            }
            return key;
        }

        private SymmetricAlgorithm GetAlgorithm(byte[] salt)
        {
            byte[] iv;
            var key = GetKey(salt, out iv);

            return new AesManaged
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = 128,
                BlockSize = 128,
                Key = key,
                IV = iv
            };
        }

        public string Decrypt(string encryptedText)
        {
            var textBytes = Convert.FromBase64String(encryptedText);
            byte[] salt;
            var aesData = ExtractSalt(textBytes, out salt);
            using (var algorithm = GetAlgorithm(salt))
            {
                using (var dec = algorithm.CreateDecryptor())
                using (var ms = new MemoryStream(aesData))
                using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
                using (var sw = new StreamReader(cs, Encoding.UTF8))
                {
                    return sw.ReadToEnd();
                }
            }
        }
    }
}
