using System;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Amazon.CloudFormation;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
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
        private bool waitForComplete;
        private string stackName;
        private string iamCapabilities;
        private bool disableRollback;

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
            Options.Add("waitForCompletion=", "True if the deployment process should wait for the stack to complete, and False otherwise.", 
                v => waitForComplete = !bool.FalseString.Equals(v, StringComparison.InvariantCultureIgnoreCase)); //True by default
            Options.Add("stackName=", "The name of the CloudFormation stack.", v => stackName = v);
            Options.Add("iamCapabilities=", "CAPABILITY_IAM if the stack requires IAM capabilities, or CAPABILITY_NAMED_IAM if the stack requires named IAM caoabilities.", v => iamCapabilities = v);
            Options.Add("disableRollback=", "True to disable the CloudFormation stack rollback on failure, and False otherwise.", 
                v => disableRollback = bool.TrueString.Equals(v, StringComparison.InvariantCultureIgnoreCase)); //False by default
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

            AmazonCloudFormationClient ClientFactory () => ClientHelpers.CreateCloudFormationClient(environment);
            StackArn StackProvider (RunningDeployment x) => new StackArn(stackName);
            ChangeSetArn ChangesetProvider (RunningDeployment x) => 
                new ChangeSetArn(x.Variables[AwsSpecialVariables.CloudFormation.Changesets.Arn]);

            var resolvedTemplate = templateResolver.Resolve(templateFile, filesInPackage, variables);
            var resolvedParameters = templateResolver.Resolve(templateParameterFile, filesInPackage, variables);
            var parameters = CloudFormationParametersFile.Create(resolvedParameters, fileSystem);
            var template = CloudFormationTemplate.Create(resolvedTemplate, parameters, fileSystem);
            var stackEventLogger = new StackEventLogger(new LogWrapper());

            var conventions = new List<IConvention>
            {
                new LogAwsUserInfoConvention(environment),
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                
                //Create or Update the stack using changesets
                new AggregateInstallationConvention(
                    new GenerateCloudFormationChangesetNameConvention(),
                    new CreateCloudFormationChangeSetConvention( ClientFactory, StackProvider, template ),
                    new DescribeCloudFormationChangeSetConvention( ClientFactory, StackProvider, ChangesetProvider),
                    new ExecuteCloudFormationChangeSetConvention(ClientFactory, StackProvider, ChangesetProvider,  stackEventLogger, waitForComplete)
                        .When(ExecuteChangesetsImmediately),
                    new CloudFormationOutputsAsVariablesConvention(ClientFactory, StackProvider, template)
                        .When(ExecuteChangesetsImmediately)
                ).When(ChangesetsEnabled),
                
                //Create or update stack using a template (no changesets)
                new  DeployAwsCloudFormationConvention(
                    template,
                    waitForComplete,
                    stackName,
                    iamCapabilities,
                    disableRollback,
                    environment).When(ChangesetsDisabled)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }

        private bool ExecuteChangesetsImmediately(RunningDeployment deployment)
        {
            return string.Compare(deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Mode], "Immediate",
                       StringComparison.OrdinalIgnoreCase) == 0;
        }

        private bool ChangesetsEnabled(RunningDeployment deployment)
        {
            return deployment.Variables.Get(SpecialVariables.Package.EnabledFeatures)
                       ?.Contains(AwsSpecialVariables.CloudFormation.Changesets.Feature) ?? false;
        }

        private bool ChangesetsDisabled(RunningDeployment deployment)
        {
            return !ChangesetsEnabled(deployment);
        }
    }
}