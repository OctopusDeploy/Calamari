using System;

namespace Calamari.Shared
{
    public interface IFileInfo
    {
        string FullPath { get; }
        string Extension { get; }
        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
    }
}