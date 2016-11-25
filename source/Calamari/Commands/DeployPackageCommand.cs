using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Features;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageCommand : Command
    {
        private string variablesFile;
        private string packageFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public DeployPackageCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            variables.Set(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath, this.packageFile);

            var feature = variables.GetFlag(SpecialVariables.Package.UpdateIisWebsite) ? "IISDeployment" : "DeployPackage";
            return new RunFeatureCommand().Execute(feature, variables);
        }
    }
}
