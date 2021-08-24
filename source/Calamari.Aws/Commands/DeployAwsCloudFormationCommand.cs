using System;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Commands;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities;

namespace Calamari.Aws.Commands
{
    [Command("deploy-aws-cloudformation", Description = "Creates a new AWS CloudFormation deployment")]
    public class DeployCloudFormationCommand : Command
    {
        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IExtractPackage extractPackage;
        PathToPackage pathToPackage;
        string templateFile;
        string templateParameterFile;
        string templateS3Url;
        string templateParameterS3Url;
        bool waitForComplete;
        string stackName;
        bool disableRollback;

        public DeployCloudFormationCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, IExtractPackage extractPackage)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            Options.Add("package=", "Path to the NuGet package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
            Options.Add("template=", "Path to the JSON template file.", v => templateFile = v);
            Options.Add("templateParameters=", "Path to the JSON template parameters file.", v => templateParameterFile = v);
            Options.Add("templateS3=", "S3 URL to the JSON template file.", v => templateS3Url = v);
            Options.Add("templateS3Parameters=", "S3 URL to the JSON template parameters file.", v => templateParameterS3Url = v);
            Options.Add("waitForCompletion=", "True if the deployment process should wait for the stack to complete, and False otherwise.",
                v => waitForComplete = !bool.FalseString.Equals(v, StringComparison.OrdinalIgnoreCase)); //True by default
            Options.Add("stackName=", "The name of the CloudFormation stack.", v => stackName = v);
            Options.Add("disableRollback=", "True to disable the CloudFormation stack rollback on failure, and False otherwise.",
                v => disableRollback = bool.TrueString.Equals(v, StringComparison.OrdinalIgnoreCase)); //False by default
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var filesInPackage = !string.IsNullOrWhiteSpace(pathToPackage);
            var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
            var templateResolver = new TemplateResolver(fileSystem);

            IAmazonCloudFormation ClientFactory () => ClientHelpers.CreateCloudFormationClient(environment);
            StackArn StackProvider (RunningDeployment x) => new StackArn(stackName);
            ChangeSetArn ChangesetProvider (RunningDeployment x) => new ChangeSetArn(x.Variables[AwsSpecialVariables.CloudFormation.Changesets.Arn]);
            string RoleArnProvider (RunningDeployment x) => x.Variables[AwsSpecialVariables.CloudFormation.RoleArn];
            var iamCapabilities = JsonConvert.DeserializeObject<List<string>>(variables.Get(AwsSpecialVariables.IamCapabilities, "[]"));
            var tags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags, "[]"));
            var allFileFormatReplacers = FileFormatVariableReplacers.BuildAllReplacers(fileSystem, log);
            var structuredConfigVariablesService = new StructuredConfigVariablesService(allFileFormatReplacers, variables, fileSystem, log);

            ICloudFormationRequestBuilder FileTemplateFactory()
            {
                var resolvedTemplate = templateResolver.Resolve(templateFile, filesInPackage, variables);
                var resolvedParameters = templateResolver.MaybeResolve(templateParameterFile, filesInPackage, variables);

                if (templateParameterFile != null && !resolvedParameters.Some())
                    throw new CommandException("Could not find template parameters file: " + templateParameterFile);

                return CloudFormationTemplate.Create(resolvedTemplate,
                                                     resolvedParameters,
                                                     fileSystem,
                                                     variables);
            }

            ICloudFormationRequestBuilder S3TemplateFactory()
            {
                if (!string.IsNullOrWhiteSpace(templateParameterS3Url) && !templateParameterS3Url.StartsWith("http"))
                    throw new CommandException("Parameters file must start with http: " + templateParameterS3Url);

                return CloudFormationS3Template.Create(templateS3Url,
                                                       templateParameterS3Url,
                                                       fileSystem,
                                                       variables,
                                                       log);
            }

            ICloudFormationRequestBuilder TemplateFactory() => string.IsNullOrWhiteSpace(templateS3Url) ? FileTemplateFactory() : S3TemplateFactory();

            var stackEventLogger = new StackEventLogger(log);

            var conventions = new List<IConvention>
            {
                new LogAwsUserInfoConvention(environment),
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService)),

                //Create or Update the stack using changesets
                new AggregateInstallationConvention(
                    new GenerateCloudFormationChangesetNameConvention(log),
                    new CreateCloudFormationChangeSetConvention( ClientFactory, stackEventLogger, StackProvider, RoleArnProvider, TemplateFactory, iamCapabilities),
                    new DescribeCloudFormationChangeSetConvention( ClientFactory, stackEventLogger, StackProvider, ChangesetProvider),
                    new ExecuteCloudFormationChangeSetConvention(ClientFactory, stackEventLogger, StackProvider, ChangesetProvider, waitForComplete)
                        .When(ImmediateChangesetExecution),
                    new CloudFormationOutputsAsVariablesConvention(ClientFactory, stackEventLogger, StackProvider)
                        .When(ImmediateChangesetExecution)
                ).When(ChangesetsEnabled),

                //Create or update stack using a template (no changesets)
                new AggregateInstallationConvention(
                        new DeployAwsCloudFormationConvention(
                            ClientFactory,
                            TemplateFactory,
                            stackEventLogger,
                            StackProvider,
                            RoleArnProvider,
                            waitForComplete,
                            stackName,
                            iamCapabilities,
                            disableRollback,
                            environment,
                            tags),
                        new CloudFormationOutputsAsVariablesConvention(ClientFactory, stackEventLogger,  StackProvider)
                )
               .When(ChangesetsDisabled)
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

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
            return deployment.Variables.Get(KnownVariables.Package.EnabledFeatures)
                       ?.Contains(AwsSpecialVariables.CloudFormation.Changesets.Feature) ?? false;
        }

        private bool ChangesetsDisabled(RunningDeployment deployment)
        {
            return !ChangesetsEnabled(deployment);
        }
    }
}