using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Aws.Commands
{
    [Command("deploy-aws-cloudformation", Description = "Creates a new AWS CloudFormation deployment")]
    public class DeployCloudFormationCommand : Command
    {
        string packageFile;
        string variablesFile;
        string sensitiveVariablesFile;
        string sensitiveVariablesPassword;
        string templateFile;
        string templateParameterFile;
        string waitForComplete;
        string action;
        string stackName;

        public DeployCloudFormationCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("template=", "Path to the JSON template file.", v => templateFile = v);
            Options.Add("templateParameters=", "Path to the JSON template parameters file.", v => templateParameterFile = v);
            Options.Add("waitForCompletion=", "True if the deployment process should wait for the stack to complete, and False otherwise.", v => waitForComplete = v);
            Options.Add("action=", "Deploy if the deployment is to deploy or update a stack, and Delete if it is to remove the stack.", v => action = v);
            Options.Add("stackName=", "The name of the CloudFormation stack.", v => stackName = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile,
                sensitiveVariablesPassword);

            var fileSystem = new WindowsPhysicalFileSystem();

            var filesInPackage = !string.IsNullOrWhiteSpace(packageFile);

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                new DeployAwsCloudFormationConvention(
                    templateFile, 
                    templateParameterFile, 
                    filesInPackage,
                    action,
                    !Boolean.FalseString.Equals(waitForComplete, StringComparison.InvariantCultureIgnoreCase),
                    stackName,
                    fileSystem)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }

    }
}