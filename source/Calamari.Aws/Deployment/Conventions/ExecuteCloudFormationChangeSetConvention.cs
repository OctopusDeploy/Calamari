
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public static class CloudFormationDefaults
    {
        public static readonly TimeSpan StatusWaitPeriod;

        static CloudFormationDefaults()
        {
            StatusWaitPeriod = TimeSpan.FromSeconds(5);
        }
    }

    public class ExecuteCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, ChangeSetArn> changeSetProvider;

        public ExecuteCloudFormationChangeSetConvention(Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider, Func<RunningDeployment, ChangeSetArn> changeSetProvider)
        {
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(changeSetProvider, "Change set provider must nobe null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");

            try
            {
                ExecuteStackChange(clientFactory, stack, changeSet);
            }
            catch (Exception e)
            {
                //Ignore?
            }
        }

        private Maybe<RunningChangeset> ExecuteStackChange(Func<AmazonCloudFormationClient> factory, StackArn stack,
            ChangeSetArn changeSet)
        {
            try
            {
                var changes =
                    factory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, stack, changeSet);

                if (changes.Changes.Count == 0)
                {
                    return Maybe<RunningChangeset>.None;
                }

                factory().ExecuteChangeSet(new ExecuteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });

                return new RunningChangeset(stack, changeSet).AsSome();
            }
            catch (AmazonCloudFormationException exception)
            {
                if (exception.Message.Contains("No updates are to be performed"))
                {
                    Log.Info("No updates are to be performed");
                    return Maybe<RunningChangeset>.None;
                }

                if (exception.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0011: The AWS account used to perform the operation does not have " +
                        "the required permissions to update the stack.\n" +
                        exception.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0011");
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0011: An unrecognised exception was thrown while updating a CloudFormation stack.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0011",
                    exception);
            }
        }
    }


    public class DescribeCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, ChangeSetArn> changeSetProvider;

        public DescribeCloudFormationChangeSetConvention(Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            Func<RunningDeployment, ChangeSetArn> changeSetProvider)
        {
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");
            var response = clientFactory.DescribeChangeSet(stack, changeSet);
            Log.SetOutputVariable("ChangeCount", response.Changes.Count.ToString(), deployment.Variables);
            Log.SetOutputVariable("Changes", JsonConvert.SerializeObject(response.Changes), deployment.Variables);
        }
    }

    public class CloudFormationExecutionContext
    {
        public class StackOutput
        {
            public string Name { get; }
            public string Value { get; }

            public StackOutput(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }

        private readonly Func<AmazonCloudFormationClient> client;
        private readonly TemplateService templateService;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ITemplateReplacement templateReplacement;
        private readonly string templateFile;
        private readonly string templateParameterFile;
        private readonly string package;

        private static readonly Regex OutputsRe = new Regex("\"?Outputs\"?\\s*:");

        public bool FilesInPackage => !string.IsNullOrWhiteSpace(package);

        public CloudFormationExecutionContext(Func<AmazonCloudFormationClient> client,
            TemplateService templateService,
            string templateFile,
            string templateParameterFile,
            string package)
        {
            this.client = client;
            this.templateService = templateService;
            this.templateFile = templateFile;
            this.templateParameterFile = templateParameterFile;
            this.package = package;
        }

        public string ResolveAndSubstituteTemplate(CalamariVariableDictionary variables)
        {
            Guard.NotNull(variables, "Variables may not be null");
            return templateService.GetSubstitutedTemplateContent(templateFile, FilesInPackage, variables);
        }

        /// <summary>
        /// Look at the template file and see if there were any outputs.
        /// </summary>
        /// <returns>true if the Outputs marker was found, and false otherwise</returns>
        public bool TemplateFileContainsOutputs(CalamariVariableDictionary variables)
        {
            Guard.NotNull(variables, "Variables may not be null");
            return templateService.GetTemplateContent(templateFile, FilesInPackage, variables)
                .Map(OutputsRe.IsMatch);
        }

        public (IReadOnlyList<StackOutput> result, bool success) GetOutputVariables(
            Func<Maybe<Stack>> query, CalamariVariableDictionary variables)
        {
            Guard.NotNull(query, "Query for stack may not be null");

            List<StackOutput> ConvertStackOutputs(Stack stack) =>
                stack.Outputs.Select(p => new StackOutput(p.OutputKey, p.OutputValue)).ToList();

            return query().Select(ConvertStackOutputs)
                .Map(result => (result: result.SomeOr(new List<StackOutput>()), success: !TemplateFileContainsOutputs(variables) || result.Some()));
        }

        public void PipeOutputs(IEnumerable<StackOutput> outputs, CalamariVariableDictionary variables, string name = "AwsOutputs")
        {
            Guard.NotNull(variables, "Variables may not be null");

            foreach (var output in outputs)
            {
                Log.SetOutputVariable($"{name}[{output.Name}]", output.Value, variables);
                Log.Info(
                    $"Saving variable \"Octopus.Action[{variables["Octopus.Action.Name"]}].Output.AwsOutputs[{output.Name}]\"");
            }
        }

        public void GetAndPipeOutputVariablesWithRetry(Func<Maybe<Stack>> query, CalamariVariableDictionary variables, bool wait, int retryCount, TimeSpan waitPeriod)
        {
            for (var retry = 0; retry < retryCount; ++retry)
            {
                var (result, success) = GetOutputVariables(query, variables);
                if (success || !wait)
                {
                    PipeOutputs(result, variables);
                    break;
                }

                // Wait for a bit for and try again
                Thread.Sleep(waitPeriod);
            }
        }

        public List<Parameter> GetParameters(CalamariVariableDictionary variables)
        {
            Guard.NotNull(variables, "variables can not be null");

            if (string.IsNullOrWhiteSpace(templateParameterFile))
            {
                return null;
            }

            return templateService.GetSubstitutedTemplateContent(templateParameterFile, FilesInPackage, variables)
                .Map(JsonConvert.DeserializeObject<List<Parameter>>);
        }

    }


    public class CreateCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly CloudFormationExecutionContext context;
        private readonly Func<RunningDeployment, StackArn> stackProvider;

        public CreateCloudFormationChangeSetConvention(Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            ICalamariFileSystem fileSystem,
            CloudFormationExecutionContext context
            )
        {
            Guard.NotNull(stackProvider, "Stack provider should not be null");
            Guard.NotNull(context, "Execution context may not be null");
            this.clientFactory = clientFactory;
            this.context = context;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            Guard.NotNull(stack, "The stack must be provided to create a change set");

            var name = $"octo-{Guid.NewGuid():N}";
            var status = clientFactory.GetStackStatus(stack, StackStatus.DoesNotExist);

            var request = CreateChangesetRequest(
                status, 
                name, 
                context.ResolveAndSubstituteTemplate(deployment.Variables), 
                stack, 
                context.GetParameters(deployment.Variables)
            );

            var result = clientFactory().CreateChangeSet(request);
            
            Log.SetOutputVariable("ChangesetId", result.Id);
            Log.SetOutputVariable("StackId", result.StackId);
        }

        public CreateChangeSetRequest CreateChangesetRequest(StackStatus status, string changesetName, string template, StackArn stack, List<Parameter> parameters)
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateBody = template,
                Parameters = parameters,
                ChangeSetName = changesetName,
                ChangeSetType = status == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE
            };
        }

        private void PollStackEventsToCompletion(Func<AmazonCloudFormationClient> clientFactory,
            StackArn stack)
        {
            clientFactory.GetLastStackEvent(stack);
        }
    }

    public class RunningChangeset
        {
            public StackArn Stack { get; }
            public ChangeSetArn ChangeSet { get; }

            public RunningChangeset(StackArn stack, ChangeSetArn changeSet)
            {
                Stack = stack;
                ChangeSet = changeSet;
            }
        }


        public class ChangeSetArn
        {
            public string Value { get; }

            public ChangeSetArn(string value)
            {
                Value = value;
            }
        }

        public class StackArn
        {
            public StackArn(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }
}