using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Substitutions;
using Octostache;

namespace Calamari.Commands
{
    [Command("deploy-azure-web", Description = "Extracts and installs a NuGet package to an Azure Web Application")]
    public class DeployAzureWebCommand : Command
    {
        private string variablesFile;
        private string packageFile;

        public DeployAzureWebCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageFile,
                "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            Log.Info("Deploying package:    " + packageFile);
            if (variablesFile != null)
                Log.Info("Using variables from: " + variablesFile);

            var variables = new VariableDictionary(variablesFile);

            var fileSystem = new CalamariPhysicalFileSystem();
            var replacer = new ConfigurationVariablesReplacer();
            var substituter = new FileSubstituter();
            var configurationTransformer =
                new ConfigurationTransformer(
                    variables.GetFlag(SpecialVariables.Package.IgnoreConfigTransformationErrors),
                    variables.GetFlag(SpecialVariables.Package.SuppressConfigTransformationLogging));

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToTemporaryDirectoryConvention(new LightweightPackageExtractor(), fileSystem),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new AzureWebAppConvention(variables),
                new DeleteStagingDirectoryConvention(fileSystem)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();

            return 0;
        }
    }
}