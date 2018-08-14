using System;
using System.IO;
using System.Security.Cryptography;
using Calamari.Shared.FileSystem;

namespace Calamari.Shared
{
    public class TemporaryFile : IDisposable
    {
        private readonly string filePath;
        private readonly ICalamariFileSystem fileSystem = CalamariFileSystem.Instance;


        public TemporaryFile(string filePath)
        {
            this.filePath = filePath;
        }

        public string DirectoryPath => "file://" + Path.GetDirectoryName(FilePath);

        public string FilePath => filePath;

        public string Hash
        {
            get
            {
                using (var file = File.OpenRead(FilePath))
                {
                    var hash = SHA1.Create().ComputeHash(file);
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
            fileSystem.DeleteFile(filePath, FailureOptions.IgnoreFailure);
        }
    }
}
