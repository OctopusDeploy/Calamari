using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Calamari.Integration.FileSystem
{
    public class TemporaryFile : IDisposable
    {
        private readonly string filePath;
        private readonly bool dispose;


        public TemporaryFile(string filePath, bool dispose = true)
        {
            this.filePath = filePath;
            this.dispose = dispose;
            Console.WriteLine(filePath);
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
                using (var file = File.OpenRead(FilePath))
                {
                    var hash = new SHA1CryptoServiceProvider().ComputeHash(file);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public long Size
        {
            get
            {
                using (var file = File.OpenRead(FilePath))
                {
                    return file.Length;
                }
            }
        }
        public void Dispose()
        {
            if (!dispose) return;
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    File.Delete(filePath);
                    if (!File.Exists(filePath))
                        return;
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
