using System;
using System.IO;

namespace Calamari.Integration.FileSystem
{
    public class FileInfoAdapter : IFileInfo
    {
        readonly FileSystemInfo info;

        public FileInfoAdapter(FileSystemInfo info)
        {
            this.info = info;
        }

        public string FullPath { get { return info.FullName; } }
        public string Extension { get { return info.Extension; } }
        public DateTime LastAccessTimeUtc { get { return info.LastAccessTimeUtc; } }
        public DateTime LastWriteTimeUtc { get { return info.LastWriteTimeUtc; } }
        public bool IsDirectory { get { return info.Attributes.HasFlag(FileAttributes.Directory); } }
    }
}