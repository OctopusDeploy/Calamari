using System;

namespace Calamari.Shared.Util
{
    public static class BinaryExtensions
    {
        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
        }
    }
}
