using System;

namespace Calamari.Integration.FileSystem
{
    public interface IFileSystemInfo
    {
        string FullPath { get; }
        string Extension { get; }
        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
        bool IsDirectory { get; }
    }
}