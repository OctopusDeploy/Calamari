using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    public class TestFile : IFile
    {
        private string BasePath { get; }
        
        public TestFile(string basePath) => BasePath = basePath;

        private string WithBase(string path) => path == null ? null : Path.Combine(BasePath, path);

        public bool Exists(string path) => File.Exists(WithBase(path));
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(WithBase(path));
        public DateTime GetCreationTime(string filePath) => File.GetCreationTime(WithBase(filePath));
        public Stream Open(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare none) =>
            File.Open(WithBase(filePath), fileMode, fileAccess, none);
        
        public void WriteAllBytes(string filePath, byte[] data) => 
            throw new NotImplementedException("Only supports read-only operations");
        public void Move(string sourceFile, string destination) => 
            throw new NotImplementedException("Only supports read-only operations");
        public void SetAttributes(string path, FileAttributes normal) => 
            throw new NotImplementedException("Only supports read-only operations");
        public void Copy(string temporaryReplacement, string originalFile, bool overwrite) =>
            throw new NotImplementedException("Only supports read-only operations");
        public void Delete(string path) => 
            throw new NotImplementedException("Only supports read-only operations");
    }
}

