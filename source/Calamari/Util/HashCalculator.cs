using System;
using System.IO;
using System.Security.Cryptography;

namespace Calamari.Util
{
    public class HashCalculator
    {
        public static byte[] Hash(Stream stream, Func<HashAlgorithm> factory)
        {
            return factory().ComputeHash(stream);
        }

        public static string Hash(Stream stream)
        {
            return DefaultAgorithm().ComputeHash(stream).ToHexString();
        }

        public static string Hash(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                return Hash(stream);
            }
        }

        public static byte[] Hash(string filename, Func<HashAlgorithm> factory)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                return Hash(stream, factory);
            }
        }

        static HashAlgorithm DefaultAgorithm()
        {
            return SHA1.Create();
        }
    }
}