using System;
using System.IO;
using System.Security.Cryptography;

namespace Calamari.Util
{
    public class HashCalculator
    {
        public static string Hash(Stream stream)
        {
            var hash = GetAlgorithm().ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static string Hash(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                return Hash(stream);
            }
        }

        static HashAlgorithm GetAlgorithm()
        {
            return SHA1.Create();
        }
    }
}