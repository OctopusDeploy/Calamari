using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Calamari.Integration.FileSystem
{
    public interface ICalamariFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        bool DirectoryIsEmpty(string path);
        void DeleteFile(string path);
        void DeleteFile(string path, FailureOptions options);
        void DeleteDirectory(string path);
        void DeleteDirectory(string path, FailureOptions options);
        IEnumerable<string> EnumerateDirectories(string parentDirectoryPath);
        IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns);
        IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns);
        long GetFileSize(string path);
        string ReadFile(string path);
        string ReadFile(string path, out Encoding encoding);
        void OverwriteFile(string path, string contents);
        void OverwriteFile(string path, string contents, Encoding encoding);
        Stream OpenFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream OpenFile(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream CreateTemporaryFile(string extension, out string path);
        string CreateTemporaryDirectory();
        int CopyDirectory(string sourceDirectory, string targetDirectory);
        int CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel);
        void CopyFile(string sourceFile, string destinationFile);
        void PurgeDirectory(string targetDirectory, FailureOptions options);
        void PurgeDirectory(string targetDirectory, FailureOptions options, CancellationToken cancel);
        void PurgeDirectory(string targetDirectory, Predicate<IFileSystemInfo> exclude, FailureOptions options);
        void EnsureDirectoryExists(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes);
        string GetFullPath(string relativeOrAbsoluteFilePath);
        void OverwriteAndDelete(string originalFile, string temporaryReplacement);
        void WriteAllBytes(string filePath, byte[] data);
        string RemoveInvalidFileNameChars(string path);
        void MoveFile(string sourceFile, string destinationFile);
        string GetRelativePath(string fromFile, string toFile);
        Stream OpenFileExclusively(string filePath, FileMode fileMode, FileAccess fileAccess);
        DateTime GetCreationTime(string filePath);
    }
}