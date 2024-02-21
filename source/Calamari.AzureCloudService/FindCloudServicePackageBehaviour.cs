using System;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureCloudService
{
    public class FindCloudServicePackageBehaviour : IAfterPackageExtractionBehaviour
    {
        readonly ICalamariFileSystem fileSystem;

        public FindCloudServicePackageBehaviour(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            context.Variables.Set(SpecialVariables.Action.Azure.CloudServicePackagePath, FindPackage(context.CurrentDirectory));

            return Task.CompletedTask;
        }

        string FindPackage(string workingDirectory)
        {
            var packages = fileSystem.EnumerateFiles(workingDirectory, "*.cspkg").ToList();

            if (packages.Count == 0)
            {
                // Try subdirectories
                packages = fileSystem.EnumerateFilesRecursively(workingDirectory, "*.cspkg").ToList();
            }

            if (packages.Count == 0)
            {
                throw new CommandException("Your package does not appear to contain any Azure Cloud Service package (.cspkg) files.");
            }

            if (packages.Count > 1)
            {
                throw new CommandException("Your deployment package contains more than one Cloud Service package (.cspkg) file, which is unsupported. Files: "
                                           + string.Concat(packages.Select(p => Environment.NewLine + " - " + p)));
            }

            return packages.Single();
        }
    }
}