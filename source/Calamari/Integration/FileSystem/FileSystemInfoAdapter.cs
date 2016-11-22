using System;
using System.IO;

namespace Calamari.Integration.FileSystem
{
    public class FileSystemInfoAdapter : IFileSystemInfo
    {
        readonly FileSystemInfo info;

        public FileSystemInfoAdapter(FileSystemInfo info)
        {
            this.info = info;
        }

        public string FullPath => info.FullName;
        public string Extension => info.Extension;
        public DateTime LastAccessTimeUtc => info.LastAccessTimeUtc;
        public DateTime LastWriteTimeUtc => info.LastWriteTimeUtc;
        public bool IsDirectory => info.Attributes.HasFlag(FileAttributes.Directory);
    }
}