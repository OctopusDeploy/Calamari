using System.Collections.Generic;
using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensibility;
using Calamari.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Azure.Commands
{
    [Command("extract-cspkg", Description = "Extracts an Azure cloud-service package (.cspkg)")]
    public class ExtractAzureCloudServicePackageCommand : Command
    {
        private string packageFile;
        private string destinationDirectory;

        public ExtractAzureCloudServicePackageCommand()
        {
            Options.Add("cspkg=", "Path to the cloud-service package", v => packageFile = Path.GetFullPath(v));
            Options.Add("destination=", "Destination directory for extracted files", v => destinationDirectory = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageFile, "No package file was specified. Please pass -cspkg YourPackage.cspkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            var variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.CloudServicePackagePath, packageFile);
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, !string.IsNullOrWhiteSpace(destinationDirectory) ? destinationDirectory : Path.GetDirectoryName(packageFile));

            var fileSystem = new WindowsPhysicalFileSystem();

            var conventions = new List<IConvention>
            {
                new EnsureCloudServicePackageIsCtpFormatConvention(fileSystem),
                new ExtractAzureCloudServicePackageConvention(fileSystem),
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}