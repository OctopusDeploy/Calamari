using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Calamari.Deployment.Conventions;
using Calamari.Deployment;

namespace Calamari.Azure.Deployment.Conventions
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
            //TODO: markse - review this, it seems too easy :/
            var configurationFiles = fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, "*.config", "*.xml");
            foreach (var configurationFile in configurationFiles)
            {
                Log.Info("Performing variable substitution on '{0}'", configurationFile);
                substituter.PerformSubstitution(configurationFile, deployment.Variables);
            }
        }
    }
}