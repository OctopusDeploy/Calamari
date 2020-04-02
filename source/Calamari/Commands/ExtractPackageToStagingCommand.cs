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
    class ExtractToStagingCommand : Command
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