using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class DeleteStagingDirectoryConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public DeleteStagingDirectoryConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            Log.VerboseFormat("Deleting '{0}'", deployment.StagingDirectory);
            fileSystem.DeleteDirectory(deployment.StagingDirectory, DeletionOptions.TryThreeTimes);
        }
    }
}