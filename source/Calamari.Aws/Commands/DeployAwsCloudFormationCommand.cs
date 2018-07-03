using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Util;

namespace Calamari.Aws.Commands
{
    [Command("deploy-aws-cloudformation", Description = "Creates a new AWS CloudFormation deployment")]
    public class DeployCloudFormationCommand : Command
    {
        private string packageFile;
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string templateFile;
        private string templateParameterFile;
        private string waitForComplete;
        private string action;
        private string stackName;
        private string iamCapabilities;
        private string disableRollback;

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
            Options.Add("iamCapabilities=", "CAPABILITY_IAM if the stack requires IAM capabilities, or CAPABILITY_NAMED_IAM if the stack requires named IAM caoabilities.", v => iamCapabilities = v);
            Options.Add("disableRollback=", "True to disable the CloudFormation stack rollback on failure, and False otherwise.", v => disableRollback = v);
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
            var environment = new AwsEnvironmentGeneration(variables);
            var templateResolver = new TemplateResolver(fileSystem);
            var templateService = new TemplateService(fileSystem, templateResolver, new TemplateReplacement(templateResolver));


            var conventions = new List<IConvention>
            {
                new LogAwsUserInfoConvention(environment),
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                new DeployAwsCloudFormationConvention(
                    templateFile, 
                    templateParameterFile, 
                    filesInPackage,
                    action,
                    !Boolean.FalseString.Equals(waitForComplete, StringComparison.InvariantCultureIgnoreCase), // true by default
                    stackName,
                    iamCapabilities,
                    Boolean.TrueString.Equals(disableRollback, StringComparison.InvariantCultureIgnoreCase), // false by default
                    templateService,
                    environment)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }

    }
}