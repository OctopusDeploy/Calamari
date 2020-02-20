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
        readonly CalamariVariableDictionary variables;
        string packageFile;

        public ExtractToStagingCommand(CalamariVariableDictionary variables)
        {
            this.variables = variables;
            Options.Add(
                "package=", 
                "Path to the package to extract.",
                v => packageFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var fileSystem = new WindowsPhysicalFileSystem();

            var conventions = new List<IConvention>
            {
                new ExtractPackageToStagingDirectoryConvention(
                    new GenericPackageExtractorFactory()
                        .createStandardGenericPackageExtractor(),
                    fileSystem,
                    null)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }
    }
}