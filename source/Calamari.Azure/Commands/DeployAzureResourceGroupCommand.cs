﻿using System.Collections.Generic;
using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Azure.Commands
{
    [Command("deploy-azure-resource-group", Description = "Creates a new Azure Resource Group deployment")]
    public class DeployAzureResourceGroupCommand : Command
    {
        string packageFile;
        string variablesFile;
        string sensitiveVariablesFile;
        string sensitiveVariablesPassword;
        string templateFile;
        string templateParameterFile;

        public DeployAzureResourceGroupCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("template=", "Path to the JSON template file.", v => templateFile = Path.GetFullPath(v));
            Options.Add("templateParameters=", "Path to the JSON template parameters file.",
                v => templateParameterFile = Path.GetFullPath(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile,
                sensitiveVariablesPassword);

            var fileSystem = new WindowsPhysicalFileSystem();

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractor(), fileSystem),
                new DeployAzureResourceGroupConvention(templateFile, templateParameterFile, fileSystem, new ResourceGroupTemplateParameterParser())
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }
    }
}