using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Commands
{
    [Command("extract-package-to-staging", Description = "Extracts a package into the staging area")]
    public class ExtractToStagingCommand : Command
    {
        readonly IExtractPackage extractPackage;
        PathToPackage pathToPackage;

        public ExtractToStagingCommand(IExtractPackage extractPackage)
        {
            this.extractPackage = extractPackage;
            Options.Add(
                "package=", 
                "Path to the package to extract.",
                v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            extractPackage.ExtractToEnvironmentCurrentDirectory(pathToPackage);

            return 0;
        }
    }
}