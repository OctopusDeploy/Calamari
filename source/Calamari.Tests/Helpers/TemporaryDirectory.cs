using System;
using System.IO;
using Calamari.Integration.FileSystem;

namespace Calamari.Tests.Helpers
{
    public class TemporaryDirectory : IDisposable
    {
        public readonly string DirectoryPath;
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        public TemporaryDirectory(string directoryPath)
        {
            this.DirectoryPath = directoryPath;
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
