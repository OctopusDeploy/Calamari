using System;
using Calamari.Integration.FileSystem;

namespace Calamari.Tests.Helpers
{
    public class TemporaryDirectory : IDisposable
    {
        private readonly string directoryPath;
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        public TemporaryDirectory(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public void Dispose()
        {
            fileSystem.DeleteDirectory(directoryPath, FailureOptions.IgnoreFailure);
        }
    }
}
