using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Iis;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Substitutions;
using Octostache;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a NuGet package")]
    public class DeployPackageCommand : Command
    {
        private string variablesFile;
        private string packageFile;

        public DeployPackageCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (string.IsNullOrWhiteSpace(packageFile))
                throw new CommandException("No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            Log.Info("Deploying package:    " + packageFile);
            if (variablesFile != null)
                Log.Info("Using variables from: " + variablesFile);

            var fileSystem = new CalamariPhysicalFileSystem();
            var scriptEngine = new ScriptEngineSelector();
            var commandLineRunner = new CommandLineRunner(new ConsoleCommandOutput());
            var replacer = new ConfigurationVariablesReplacer();
            var substituter = new FileSubstituter();
            var configurationTransformer = new ConfigurationTransformer();
            var iis = new InternetInformationServer();

            var variables = new VariableDictionary(variablesFile);
            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new ExtractPackageToApplicationDirectoryConvention(new LightweightPackageExtractor(), fileSystem),
                new DeployScriptConvention("PreDeploy", fileSystem, scriptEngine, commandLineRunner),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new AzureConfigurationConvention(),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new DeployScriptConvention("Deploy", fileSystem, scriptEngine, commandLineRunner),
                new LegacyIisWebSiteConvention(fileSystem, iis),
                new AzureUploadConvention(),
                new AzureDeploymentConvention(),
                new DeployScriptConvention("PostDeploy", fileSystem, scriptEngine, commandLineRunner)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}
