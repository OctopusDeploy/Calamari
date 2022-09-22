using System;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class BinaryExtensions
    {
        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
        }
    }
}