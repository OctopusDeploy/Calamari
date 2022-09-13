using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Calamari.Common.Plumbing.Extensions
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

        /// <summary>
        /// When FIPS mode is enabled certain algorithms such as MD5 may not be available.
        /// </summary>
        /// <param name="factory">A callback function used to create the associated algorithm i.e. MD5.Create</param>
        /// <returns></returns>
        public static bool IsAvailableHashingAlgorithm(Func<HashAlgorithm> factory)
        {
            Guard.NotNull(factory, "Factory method is required");

            try
            {
                var result = factory();
                return result != null;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }
    }
}