using System;
using System.IO;
using System.Security.Cryptography;
using Calamari.Integration.FileSystem;

namespace Calamari.Util
{
    public static class MD5HashHelper
    {

        static byte[] GetFileStreamChecksum(ICalamariFileSystem fileSystem, string path, Func<Stream, byte[]> algorithm)
        {
            using (var stream = fileSystem.OpenFile(path, FileAccess.Read))
            {
                return algorithm(stream);
            }
        }

        public static byte[] GetFileMd5Checksum(ICalamariFileSystem filesystem, string path)
        {
            return GetFileStreamChecksum(filesystem, path, stream => new MD5CryptoServiceProvider().ComputeHash(stream));
        }
    }
}
