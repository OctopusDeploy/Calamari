using System;
using System.IO;
using System.Security.Cryptography;

namespace Calamari.Util
{
    class HashCalculator
    {
        public static string Hash(Stream stream)
        {
            var hash = GetAlgorithm().ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static HashAlgorithm GetAlgorithm()
        {
            return new SHA1CryptoServiceProvider();
        }
    }
}