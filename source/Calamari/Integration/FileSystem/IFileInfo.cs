using System;

namespace Calamari.Integration.FileSystem
{
    public interface IFileInfo
    {
        string FullPath { get; }
        string Extension { get; }
        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
    }
}