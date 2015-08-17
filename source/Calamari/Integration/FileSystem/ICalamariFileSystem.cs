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
        void DeleteFile(string path, DeletionOptions options);
        void DeleteDirectory(string path);
        void DeleteDirectory(string path, DeletionOptions options);
        IEnumerable<string> EnumerateDirectories(string parentDirectoryPath);
        IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns);
        IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns);
        long GetFileSize(string path);
        string ReadFile(string path);
        void AppendToFile(string path, string contents);
        void OverwriteFile(string path, string contents);
        void OverwriteFile(string path, string contents, Encoding encoding);
        Stream OpenFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream OpenFile(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream CreateTemporaryFile(string extension, out string path);
        string CreateTemporaryDirectory();
        void CopyDirectory(string sourceDirectory, string targetDirectory, int overwriteFileRetryAttempts = 6);
        void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel, int overwriteFileRetryAttempts = 6);
        void CopyFile(string sourceFile, string destinationFile, int overwriteFileRetryAttempts = 6);
        void PurgeDirectory(string targetDirectory, DeletionOptions options);
        void PurgeDirectory(string targetDirectory, DeletionOptions options, CancellationToken cancel);
        void PurgeDirectory(string targetDirectory, Predicate<IFileInfo> filter, DeletionOptions options);
        void EnsureDirectoryExists(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes);
        string GetFullPath(string relativeOrAbsoluteFilePath);
        void OverwriteAndDelete(string originalFile, string temporaryReplacement);
        void WriteAllBytes(string filePath, byte[] data);
        string RemoveInvalidFileNameChars(string path);
        void MoveFile(string sourceFile, string destinationFile);
        Encoding GetFileEncoding(string path);
    }
}