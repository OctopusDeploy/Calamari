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
using Amazon.CloudFormation;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
using Calamari.Util;
using Octopus.CoreUtilities;

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

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var filesInPackage = !string.IsNullOrWhiteSpace(packageFile);
            var environment = AwsEnvironmentGeneration.Create(variables).GetAwaiter().GetResult();
            var templateResolver = new TemplateResolver(fileSystem);

            IAmazonCloudFormation ClientFactory () => ClientHelpers.CreateCloudFormationClient(environment);
            StackArn StackProvider (RunningDeployment x) => new StackArn(stackName);
            ChangeSetArn ChangesetProvider (RunningDeployment x) => new ChangeSetArn(x.Variables[AwsSpecialVariables.CloudFormation.Changesets.Arn]);
            string RoleArnProvider (RunningDeployment x) => x.Variables[AwsSpecialVariables.CloudFormation.RoleArn];

            CloudFormationTemplate TemplateFactory()
            {
                var resolvedTemplate = templateResolver.Resolve(templateFile, filesInPackage, variables);
                var resolvedParameters = templateResolver.MaybeResolve(templateParameterFile, filesInPackage, variables);
                
                if (templateParameterFile != null && !resolvedParameters.Some())
                    throw new CommandException("Could not find template parameters file: " + templateParameterFile);
                
                var parameters = CloudFormationParametersFile.Create(resolvedParameters, fileSystem, variables);
                return CloudFormationTemplate.Create(resolvedTemplate, parameters, fileSystem, variables);
            }

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
                    new CreateCloudFormationChangeSetConvention( ClientFactory, stackEventLogger, StackProvider, RoleArnProvider, TemplateFactory, iamCapabilities),
                    new DescribeCloudFormationChangeSetConvention( ClientFactory, stackEventLogger, StackProvider, ChangesetProvider),
                    new ExecuteCloudFormationChangeSetConvention(ClientFactory, stackEventLogger, StackProvider, ChangesetProvider, waitForComplete)
                        .When(ImmediateChangesetExecution),
                    new CloudFormationOutputsAsVariablesConvention(ClientFactory, stackEventLogger, StackProvider, () => TemplateFactory().HasOutputs)
                        .When(ImmediateChangesetExecution)
                ).When(ChangesetsEnabled),
             
                //Create or update stack using a template (no changesets)
                new AggregateInstallationConvention(
                        new  DeployAwsCloudFormationConvention(
                            ClientFactory,
                            TemplateFactory,
                            stackEventLogger,
                            StackProvider,
                            RoleArnProvider,
                            waitForComplete,
                            stackName,
                            iamCapabilities,
                            disableRollback,
                            environment),
                        new CloudFormationOutputsAsVariablesConvention(ClientFactory, stackEventLogger,  StackProvider, () => TemplateFactory().HasOutputs)
                )
               .When(ChangesetsDisabled)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }

        private bool ChangesetsDeferred(RunningDeployment deployment)
        {
            return string.Compare(deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Defer], bool.TrueString,
                       StringComparison.OrdinalIgnoreCase) == 0;
        }

        private bool ImmediateChangesetExecution(RunningDeployment deployment)
        {
            return !ChangesetsDeferred(deployment);
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