using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Calamari.Util
{
    public class HashCalculator
    {
        public static string Hash(Stream stream)
        {
            var hash = GetAlgorithm().ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static HashAlgorithm GetAlgorithm()
        {
            return SHA1.Create();
        }

        public static string SHA256Hash(string input)
        {
            using (var hashAlgo = SHA256.Create())
            {
                var utf8Bytes = Encoding.UTF8.GetBytes(input);
                var sha1Hash = hashAlgo.ComputeHash(utf8Bytes);
                return Convert.ToBase64String(sha1Hash);
            }
        }
    }
}