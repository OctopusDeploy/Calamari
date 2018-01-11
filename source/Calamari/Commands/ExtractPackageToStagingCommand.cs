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
        string packageFile;
        string variablesFile;
        string sensitiveVariablesFile;
        string sensitiveVariablesPassword;

        public ExtractToStagingCommand()
        {
            Options.Add(
                "variables=",
                "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add(
                "sensitiveVariables=",
                "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add(
                "sensitiveVariablesPassword=",
                "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
            Options.Add(
                "package=", 
                "Path to the package to extract.",
                v => packageFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            var variables = new CalamariVariableDictionary(
                variablesFile,
                sensitiveVariablesFile,
                sensitiveVariablesPassword);

            var fileSystem = new WindowsPhysicalFileSystem();

            var conventions = new List<IConvention>
            {
                new ExtractPackageToStagingDirectoryConvention(
                    new GenericPackageExtractorFactory()
                        .createStandardGenericPackageExtractor(),
                    fileSystem)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }
    }
}