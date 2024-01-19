using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureServiceFabric.Behaviours
{
    class SubstituteVariablesInAzureServiceFabricPackageBehaviour : IDeployBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public SubstituteVariablesInAzureServiceFabricPackageBehaviour(ICalamariFileSystem fileSystem, IFileSubstituter substituter)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var configurationFiles = fileSystem.EnumerateFilesRecursively(context.CurrentDirectory, "*.config", "*.xml");
            foreach (var configurationFile in configurationFiles)
            {
                substituter.PerformSubstitution(configurationFile, context.Variables);
            }

            return Task.CompletedTask;
        }
    }
}