using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInFilesConvention : IInstallConvention
    {
        private readonly ICalamariFileSystem fileSystem;

        public SubstituteInFilesConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            throw new System.NotImplementedException();
        }
    }
}