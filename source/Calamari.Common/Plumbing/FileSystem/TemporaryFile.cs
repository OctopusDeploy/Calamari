using System;
using System.IO;
using System.Security.Cryptography;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class TemporaryFile : IDisposable
    {
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        public TemporaryFile(string filePath)
        {
            this.FilePath = filePath;
        }

        public string DirectoryPath => "file://" + Path.GetDirectoryName(FilePath);

        public string FilePath { get; }

        public string Hash
        {
            get
            {
                using (var file = fileSystem.OpenFile(FilePath, FileAccess.Read))
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
                using (var file = fileSystem.OpenFile(FilePath, FileAccess.Read))
                {
                    return file.Length;
                }
            }
        }

        public void Dispose()
        {
            fileSystem.DeleteFile(FilePath, FailureOptions.IgnoreFailure);
        }
    }
}