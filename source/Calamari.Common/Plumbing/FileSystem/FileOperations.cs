using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Common.Plumbing.FileSystem
{
    public interface IFile
    {
        void Copy(string temporaryReplacement, string originalFile, bool overwrite);
        void Delete(string path);
        bool Exists(string? path);
        byte[] ReadAllBytes(string path);
        void WriteAllBytes(string filePath, byte[] data);
        void Move(string sourceFile, string destination);
        void SetAttributes(string path, FileAttributes normal);
        DateTime GetCreationTime(string filePath);
        Stream Open(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare none);
    }

    public interface IDirectory
    {
        void CreateDirectory(string path);
        void Delete(string path, bool recursive);
        bool Exists(string? path);
        string[] GetFileSystemEntries(string path);
        IEnumerable<string> EnumerateDirectories(string path);
        IEnumerable<string> EnumerateDirectoriesRecursively(string path);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath,
            params string[] searchPatterns);
        IEnumerable<string> EnumerateFilesRecursively(
            string parentDirectoryPath,
            params string[] searchPatterns);
        IEnumerable<string> GetFiles(string sourceDirectory, string s);
        IEnumerable<string> GetDirectories(string path);
        string GetCurrentDirectory();
    }

    public class StandardFile : IFile
    {
        public void Delete(string path)
        {
            FileAccessChecker.IsFileLocked(() => File.Delete(path));
        }

        public bool Exists(string? path)
        {
            return FileAccessChecker.IsFileLocked(() => File.Exists(path));
        }

        public byte[] ReadAllBytes(string path)
        {
            return FileAccessChecker.IsFileLocked(() => File.ReadAllBytes(path));
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            FileAccessChecker.IsFileLocked(() => File.WriteAllBytes(path, bytes));
        }

        public void Move(string source, string destination)
        {
            FileAccessChecker.IsFileLocked(() => File.Move(source, destination));
        }

        public void SetAttributes(string path, FileAttributes fileAttributes)
        {
            FileAccessChecker.IsFileLocked(() => File.SetAttributes(path, fileAttributes));
        }

        public DateTime GetCreationTime(string path)
        {
            return FileAccessChecker.IsFileLocked<DateTime>(() => File.GetCreationTime(path));
        }

        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return FileAccessChecker.IsFileLocked<Stream>(() => File.Open(path, mode, access, share));
        }

        public void Copy(string source, string destination, bool overwrite)
        {
            FileAccessChecker.IsFileLocked(() => File.Copy(source, destination, overwrite));
        }
    }

    public class StandardDirectory : IDirectory
    {
        public void CreateDirectory(string path)
        {
            FileAccessChecker.IsFileLocked(() => Directory.CreateDirectory(path));
        }

        public void Delete(string path, bool recursive)
        {
            FileAccessChecker.IsFileLocked(() => Directory.Delete(path, recursive));
        }

        public bool Exists(string? path)
        {
            return FileAccessChecker.IsFileLocked(() => Directory.Exists(path));
        }

        public string[] GetFileSystemEntries(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Directory.GetFileSystemEntries(path));
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Directory.EnumerateDirectories(path));
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories));
        }

        public virtual IEnumerable<string> EnumerateFiles(
            string parentDirectoryPath,
            params string[] searchPatterns)
        {
            return FileAccessChecker.IsFileLocked(() => EnumerateFiles(parentDirectoryPath, SearchOption.TopDirectoryOnly, searchPatterns));
        }

        public virtual IEnumerable<string> EnumerateFilesRecursively(
            string parentDirectoryPath,
            params string[] searchPatterns)
        {
            return FileAccessChecker.IsFileLocked(() => EnumerateFiles(parentDirectoryPath, SearchOption.AllDirectories, searchPatterns));
        }

        private IEnumerable<string> EnumerateFiles(
            string parentDirectoryPath,
            SearchOption searchOption,
            string[] searchPatterns)
        {
            return searchPatterns.Length == 0
                ? FileAccessChecker.IsFileLocked(() => Directory.EnumerateFiles(parentDirectoryPath, "*", searchOption))
                : searchPatterns.SelectMany(pattern =>
                                                FileAccessChecker.IsFileLocked(() => Directory.EnumerateFiles(parentDirectoryPath, pattern, searchOption)).Distinct());
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern)
        {
            return FileAccessChecker.IsFileLocked(() => Directory.GetFiles(path, searchPattern));
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Directory.GetDirectories(path));
        }

        public string GetCurrentDirectory()
        {
            return FileAccessChecker.IsFileLocked(() => Directory.GetCurrentDirectory());
        }
    }

#if USE_ALPHAFS_FOR_LONG_FILE_PATH_SUPPORT
    public class LongPathsFile : IFile
    {
        public void Delete(string path)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.Delete(path));
        }

        public bool Exists(string? path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.Exists(path));
        }

        public byte[] ReadAllBytes(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path));
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.WriteAllBytes(path, bytes));
        }

        public void Move(string source, string destination)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.Move(source, destination));
        }

        public void SetAttributes(string path, FileAttributes fileAttributes)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.SetAttributes(path, fileAttributes));
        }

        public DateTime GetCreationTime(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.GetCreationTime(path));
        }

        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.Open(path, mode, access, share));
        }

        public void Copy(string source, string destination, bool overwrite)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.File.Copy(source, destination, overwrite));
        }
    }

    public class LongPathsDirectory : IDirectory
    {
        public void CreateDirectory(string path)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(path));
        }

        public void Delete(string path, bool recursive)
        {
            FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.Directory.Delete(path, recursive));
        }

        public bool Exists(string? path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.Directory.Exists(path));
        }

        public string[] GetFileSystemEntries(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.Directory.GetFileSystemEntries(path));
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(path));
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string path)
        {
            return FileAccessChecker.IsFileLocked(() => Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories));
        }

        public IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            return FileAccessChecker.IsFileLocked(() => EnumerateFiles(parentDirectoryPath, SearchOption.TopDirectoryOnly, searchPatterns));
        }

        public IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns)
        {
            return FileAccessChecker.IsFileLocked(() => EnumerateFiles(parentDirectoryPath, SearchOption.AllDirectories, searchPatterns));
        }

        private IEnumerable<string> EnumerateFiles(
            string parentDirectoryPath,
            SearchOption searchOption,
            string[] searchPatterns)
        {
            // Note we aren't using Alphaleonis.Win32.Filesystem.Directory.EnumerateFiles which handles long file paths due to performance issues.
            var parentDirectoryInfo = new DirectoryInfo(parentDirectoryPath);

            return searchPatterns.Length == 0
                ? parentDirectoryInfo.GetFiles("*", searchOption).Select(fi => fi.FullName)
                : searchPatterns.SelectMany(pattern => parentDirectoryInfo.GetFiles(pattern, searchOption).Select(fi => fi.FullName)).Distinct();
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern)
        {
            return Alphaleonis.Win32.Filesystem.Directory.GetFiles(path, searchPattern);
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return Alphaleonis.Win32.Filesystem.Directory.GetDirectories(path);
        }

        public string GetCurrentDirectory()
        {
            return Alphaleonis.Win32.Filesystem.Directory.GetCurrentDirectory();
        }
    }
#endif
}