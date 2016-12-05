using System;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.FileSystem;
using FailureOptions = Calamari.Extensibility.FileSystem.FailureOptions;

namespace Calamari.IntegrationTests.Helpers
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
