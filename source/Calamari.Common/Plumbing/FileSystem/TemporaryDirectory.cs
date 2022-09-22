using System;
using System.IO;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class TemporaryDirectory : IDisposable
    {
        public readonly string DirectoryPath;
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        public TemporaryDirectory(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public void Dispose()
        {
            fileSystem.DeleteDirectory(DirectoryPath, FailureOptions.IgnoreFailure);
        }

        public static TemporaryDirectory Create()
        {
            var dir = Path.Combine(Path.GetTempPath(), "Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return new TemporaryDirectory(dir);
        }
    }
}