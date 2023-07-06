using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;

namespace Calamari.Common.Plumbing.FileSystem
{
    public interface ICalamariFileSystem
    {
        bool FileExists([NotNullWhen(true)]string? path);
        bool DirectoryExists(string? path);
        bool DirectoryIsEmpty(string path);
        void DeleteFile(string path, FailureOptions options = FailureOptions.ThrowOnFailure);
        void DeleteDirectory(string path);
        void DeleteDirectory(string path, FailureOptions options);
        IEnumerable<string> EnumerateDirectories(string parentDirectoryPath);
        IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath);

        /// <summary>
        /// Enumerates files on a given path with the given glob patterns.
        /// </summary>
        /// <returns>An enumeration of the relative paths of the files found.</returns>
        /// <remarks>
        /// Note that unlike <see cref="EnumerateFullPathFilesWithGlob"/>, <see cref="globPatterns"/> must be relative to
        /// <see cref="parentDirectoryPath"/>.
        /// <br/>
        /// Results are RELATIVE paths, not full paths.
        /// </remarks>
        IEnumerable<string> EnumerateFilesWithGlob(string parentDirectoryPath, params string[] globPatterns);

        /// <summary>
        /// Enumerates files on a given path with the given glob patterns.
        /// </summary>
        /// <returns>An enumeration of the full paths of the files found.</returns>
        /// <remarks>
        /// <see cref="globPatterns"/> can be full paths to files or full paths with glob patterns (as well as relative paths).
        /// If they are full paths, the parentDirectoryPath is ignored for that glob pattern.
        /// <br/>
        /// Results are FULL paths, not relative paths.
        /// </remarks>
        IEnumerable<string> EnumerateFullPathFilesWithGlob(string parentDirectoryPath, params string[] globPatterns);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns);
        IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns);
        long GetFileSize(string path);
        string ReadFile(string path);
        string ReadFile(string path, out Encoding encoding);
        string ReadAllText(byte[] bytes, out Encoding encoding, ICollection<Encoding> encodingPrecedence);
        void OverwriteFile(string path, string contents, Encoding? encoding = null);
        void OverwriteFile(string path, Action<TextWriter> writeToWriter, Encoding? encoding = null);
        void OverwriteFile(string path, byte[] data);
        void WriteAllText(string path, string contents, Encoding? encoding = null);
        void WriteAllText(string path, Action<TextWriter> writeToWriter, Encoding? encoding = null);
        Stream OpenFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream OpenFile(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream CreateTemporaryFile(string extension, out string path);
        string CreateTemporaryDirectory();
        void CreateDirectory(string directory);
        int CopyDirectory(string sourceDirectory, string targetDirectory);
        int CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel);
        void CopyFile(string sourceFile, string destinationFile);
        void PurgeDirectory(string targetDirectory, FailureOptions options);
        void PurgeDirectory(string targetDirectory, FailureOptions options, CancellationToken cancel);
        void PurgeDirectory(string targetDirectory, Predicate<FileSystemInfo> exclude, FailureOptions options);
        void PurgeDirectory(string targetDirectory, FailureOptions options, params string[] globs);
        void EnsureDirectoryExists(string directoryPath);
        bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes);
        bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes);
        string GetFullPath(string relativeOrAbsoluteFilePath);
        void OverwriteAndDelete(string originalFile, string temporaryReplacement);
        void WriteAllBytes(string filePath, byte[] data);
        string RemoveInvalidFileNameChars(string path);
        void MoveFile(string sourceFile, string destinationFile);
        string GetRelativePath(string fromFile, string toFile);
        Stream OpenFileExclusively(string filePath, FileMode fileMode, FileAccess fileAccess);
        DateTime GetCreationTime(string filePath);
        string GetFileName(string filePath);
        string GetDirectoryName(string directoryPath);
        byte[] ReadAllBytes(string filePath);
    }
}