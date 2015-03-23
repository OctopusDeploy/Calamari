using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Calamari.Integration.FileSystem
{
    public class TemporaryFile : IDisposable
    {
        private readonly string filePath;

        public TemporaryFile(string filePath)
        {
            this.filePath = filePath;
        }

        public string DirectoryPath
        {
            get { return Path.GetDirectoryName(FilePath); }
        }

        public string FilePath
        {
            get { return filePath; }
        }

        public string Hash
        {
            get
            {
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var hash = new SHA1CryptoServiceProvider().ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        public void Dispose()
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
