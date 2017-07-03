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

        /// <summary>
        /// Generated the SHA256 hash of a string encoded to Base64
        /// </summary>
        /// <param name="input">The string to get the hash for</param>
        /// <returns>The Base64 encoded hash code</returns>
        public static string SHA256Hash(string input)
        {
            Guard.NotNullOrWhiteSpace(input, "String to generate hash for can not be empty");
            
#if CAPI_AES
            var hashAlgo = new SHA256Managed();
#else
            var hashAlgo = SHA256.Create();
#endif
            using (hashAlgo)
            {
                var utf8Bytes = Encoding.UTF8.GetBytes(input);
                var sha1Hash = hashAlgo.ComputeHash(utf8Bytes);
                return Convert.ToBase64String(sha1Hash);
            }
        }
    }
}
