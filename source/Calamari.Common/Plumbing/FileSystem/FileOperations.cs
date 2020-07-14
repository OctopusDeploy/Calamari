using System;
using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Plumbing.FileSystem
{
    public interface IFile
    {
        void Copy(string temporaryReplacement, string originalFile, bool overwrite);
        void Delete(string path);
        bool Exists(string path);
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
        bool Exists(string path);
        string[] GetFileSystemEntries(string path);
        IEnumerable<string> EnumerateDirectories(string path);
        IEnumerable<string> EnumerateDirectoriesRecursively(string path);
        IEnumerable<string> GetFiles(string sourceDirectory, string s);
        IEnumerable<string> GetDirectories(string path);
        string GetCurrentDirectory();
    }

    public class StandardFile : IFile
    {
        public void Delete(string path)
        {
            File.Delete(path);
        }

        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        public void Move(string source, string destination)
        {
            File.Move(source, destination);
        }

        public void SetAttributes(string path, FileAttributes fileAttributes)
        {
            File.SetAttributes(path, fileAttributes);
        }

        public DateTime GetCreationTime(string path)
        {
            return File.GetCreationTime(path);
        }

        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return File.Open(path, mode, access, share);
        }

        public void Copy(string source, string destination, bool overwrite)
        {
            File.Copy(source, destination, overwrite);
        }
    }

    public class StandardDirectory : IDirectory
    {
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void Delete(string path, bool recursive)
        {
            Directory.Delete(path, recursive);
        }

        public bool Exists(string path)
        {
            return Directory.Exists(path);
        }

        public string[] GetFileSystemEntries(string path)
        {
            return Directory.GetFileSystemEntries(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return Directory.EnumerateDirectories(path);
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string path)
        {
            return Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories);
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern);
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return Directory.GetDirectories(path);
        }

        public string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }
    }

#if USE_ALPHAFS_FOR_LONG_FILE_PATH_SUPPORT
    public class LongPathsFile : IFile
    {
        public void Delete(string path)
        {
            Alphaleonis.Win32.Filesystem.File.Delete(path);
        }

        public bool Exists(string path)
        {
            return Alphaleonis.Win32.Filesystem.File.Exists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            return Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path);
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            Alphaleonis.Win32.Filesystem.File.WriteAllBytes(path, bytes);
        }

        public void Move(string source, string destination)
        {
            Alphaleonis.Win32.Filesystem.File.Move(source, destination);
        }

        public void SetAttributes(string path, FileAttributes fileAttributes)
        {
            Alphaleonis.Win32.Filesystem.File.SetAttributes(path, fileAttributes);
        }

        public DateTime GetCreationTime(string path)
        {
            return Alphaleonis.Win32.Filesystem.File.GetCreationTime(path);
        }

        public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return Alphaleonis.Win32.Filesystem.File.Open(path, mode, access, share);
        }

        public void Copy(string source, string destination, bool overwrite)
        {
            Alphaleonis.Win32.Filesystem.File.Copy(source, destination, overwrite);
        }
    }

    public class LongPathsDirectory : IDirectory
    {
        public void CreateDirectory(string path)
        {
            Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(path);
        }

        public void Delete(string path, bool recursive)
        {
            Alphaleonis.Win32.Filesystem.Directory.Delete(path, recursive);
        }

        public bool Exists(string path)
        {
            return Alphaleonis.Win32.Filesystem.Directory.Exists(path);
        }

        public string[] GetFileSystemEntries(string path)
        {
            return Alphaleonis.Win32.Filesystem.Directory.GetFileSystemEntries(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(path);
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string path)
        {
            return Alphaleonis.Win32.Filesystem.Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories);
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