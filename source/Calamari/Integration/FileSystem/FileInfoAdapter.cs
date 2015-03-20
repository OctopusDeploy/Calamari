using System;
using System.IO;

namespace Calamari.Integration.FileSystem
{
    public class FileInfoAdapter : IFileInfo
    {
        readonly FileInfo info;

        public FileInfoAdapter(FileInfo info)
        {
            this.info = info;
        }

        public string FullPath { get { return info.FullName; } }
        public string Extension { get { return info.Extension; } }
        public DateTime LastAccessTimeUtc { get { return info.LastAccessTimeUtc; } }
        public DateTime LastWriteTimeUtc { get { return info.LastWriteTimeUtc; } }
    }
}