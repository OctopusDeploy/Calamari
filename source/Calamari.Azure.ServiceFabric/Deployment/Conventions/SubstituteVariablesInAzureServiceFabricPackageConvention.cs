using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Azure.ServiceFabric.Deployment.Conventions
{
    public class SubstituteVariablesInAzureServiceFabricPackageConvention : IInstallConvention
    {
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public SubstituteVariablesInAzureServiceFabricPackageConvention(ICalamariFileSystem fileSystem, IFileSubstituter substituter)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
        }

        public void Install(RunningDeployment deployment)
        {
            var configurationFiles = fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, "*.config", "*.xml");
            foreach (var configurationFile in configurationFiles)
            {
                substituter.PerformSubstitution(configurationFile, deployment.Variables);
            }
        }
    }
}