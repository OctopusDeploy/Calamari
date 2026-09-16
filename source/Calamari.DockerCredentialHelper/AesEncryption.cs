using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Calamari.DockerCredentialHelper
{
    // Self-contained AES used only by docker-credential-octopus to protect the short-lived credential
    // files it writes during a package acquisition. The helper both writes (on `docker login`) and
    // reads (on `docker pull`) these files, so this does not need to interoperate with Calamari's AesEncryption
    public class AesEncryption
    {
        const int KeySizeBits = 256;
        const int BlockSizeBits = 128;
        const int PasswordSaltIterations = 1000;
        static readonly byte[] PasswordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");
        static readonly byte[] IvPrefix = Encoding.UTF8.GetBytes("IV__");

        readonly byte[] encryptionKey;

        AesEncryption(string password)
        {
            encryptionKey = Rfc2898DeriveBytes.Pbkdf2(password, PasswordPaddingSalt, PasswordSaltIterations, HashAlgorithmName.SHA1, KeySizeBits / 8);
        }

        public static AesEncryption ForScripts(string password) => new AesEncryption(password);

        public byte[] Encrypt(string plaintext)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plaintext);
            using var algorithm = CreateAlgorithm();
            using var encryptor = algorithm.CreateEncryptor();
            using var stream = new MemoryStream();

            // The IV is random per-encrypt and prepended (after a marker) so Decrypt can recover it.
            stream.Write(IvPrefix, 0, IvPrefix.Length);
            stream.Write(algorithm.IV, 0, algorithm.IV.Length);
            using (var cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);

            return stream.ToArray();
        }

        public string Decrypt(byte[] encrypted)
        {
            var aesBytes = ExtractIV(encrypted, out var iv);
            using var algorithm = CreateAlgorithm();
            algorithm.IV = iv;
            using var decryptor = algorithm.CreateDecryptor();
            using var memoryStream = new MemoryStream(aesBytes);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        Aes CreateAlgorithm()
        {
            var algorithm = Aes.Create();
            algorithm.Mode = CipherMode.CBC;
            algorithm.Padding = PaddingMode.PKCS7;
            algorithm.KeySize = KeySizeBits;
            algorithm.BlockSize = BlockSizeBits;
            algorithm.Key = encryptionKey;
            return algorithm;
        }

        static byte[] ExtractIV(byte[] encrypted, out byte[] iv)
        {
            var ivLength = BlockSizeBits / 8;
            iv = new byte[ivLength];
            Buffer.BlockCopy(encrypted, IvPrefix.Length, iv, 0, ivLength);

            var ivDataLength = IvPrefix.Length + ivLength;
            var aesDataLength = encrypted.Length - ivDataLength;
            var aesData = new byte[aesDataLength];
            Buffer.BlockCopy(encrypted, ivDataLength, aesData, 0, aesDataLength);
            return aesData;
        }
    }
}
