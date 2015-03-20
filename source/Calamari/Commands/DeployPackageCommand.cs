using System.Collections.Generic;
using Calamari.Commands.Support;
using Octopus.Deploy.PackageInstaller;
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
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = v);
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            //var packageFilePath = EnsureExists(MapPath(args[0]));
            //var variablesFilePath = EnsureExists(MapPath(args[1]));

            //var variables = new VariableDictionary(variablesFilePath);

            //var conventions = new List<IConvention>
            //    {
            //        new ExtractPackageToTemporaryDirectoryConvention(),
            //        new DeployScriptConvention("PreDeploy"),
            //        new DeletePackageFileConvention(),
            //        new SubstituteInFilesConvention(),
            //        new ConfigurationTransformsConvention(),
            //        new ConfigurationVariablesConvention(),
            //        new AzureConfigurationConvention(),
            //        new CopyPackageToCustomInstallationDirectoryConvention(),
            //        new DeployScriptConvention("Deploy"),
            //        new LegacyIisWebSiteConvention(),
            //        new AzureUploadConvention(),
            //        new AzureDeploymentConvention(),
            //        new DeployScriptConvention("PostDeploy")
            //    };

            //var deployment = new RunningDeployment(packageFilePath, variables);
            //var conventionRunner = new ConventionProcessor(deployment, conventions);
            //conventionRunner.RunConventions();

            return 0;
        }
    }
}
