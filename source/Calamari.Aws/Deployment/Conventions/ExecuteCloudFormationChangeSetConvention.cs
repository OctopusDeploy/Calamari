
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration;
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
        public static readonly int RetryCount = 3;

        static CloudFormationDefaults()
        {
            StatusWaitPeriod = TimeSpan.FromSeconds(5);
        }
    }

    public interface ITemplate
    {
        string Content { get; }
    }

    public interface ITemplateInputs<TInput>
    {
        IReadOnlyList<TInput> Inputs { get; }
    }

    public interface ITemplateOutputs<TOutput>
    {
        bool HasOutputs { get; }
        IReadOnlyList<TOutput> Outputs { get; }
    }
    
    public class StackFormationNamedOutput
    {
        public string Name { get; }

        public StackFormationNamedOutput(string name)
        {
            Name = name;
        }
    }

    public class DeleteCloudFormationStackConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly StackEventLogger logger;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly IAwsEnvironmentGeneration environment;
        private readonly bool waitForComplete;

        public DeleteCloudFormationStackConvention(
            IAwsEnvironmentGeneration environment,
            StackEventLogger logger,
            Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            bool waitForComplete
            )
        {
            Guard.NotNull(clientFactory, "Client must not be null");
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(environment, "Aws environment generation may not be null");
            
            
            this.clientFactory = clientFactory;
            this.logger = logger;
            this.stackProvider = stackProvider;
            this.waitForComplete = waitForComplete;
            this.environment = environment;
        }
        
        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            var stack = stackProvider(deployment);

            if (clientFactory.GetStackStatus(stack, StackStatus.Completed) != StackStatus.DoesNotExist)
            {
               DeleteCloudFormation(stack);
            }
            else
            {
                Log.Info(
                    $"No stack called {stack.Value} exists in region {environment.AwsRegion.SystemName}");
            }

            if (waitForComplete)
            {
                clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, status =>
                {
                    logger.Log(status);
                    logger.LogRollbackError(status, x => clientFactory.GetLastStackEvent(stack, x), true, false);
                    return status.ToMaybe().SelectValueOr(x => 
                        (x.ResourceStatus.Value.EndsWith("_COMPLETE") ||
                         x.ResourceStatus.Value.EndsWith("_FAILED")) &&
                        x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);
                });
            }
        }

        private void DeleteCloudFormation(StackArn stack)
        {
            try
            {
                clientFactory()
                    // Client becomes the API response
                    .Map(client => client.DeleteStack(new DeleteStackRequest {StackName = stack.Value}))
                    // Log the response details
                    .Tee(status =>
                        Log.Info(
                            $"Deleted stack called {stack.Value} in region {environment.AwsRegion.SystemName}"));
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0009: The AWS account used to perform the operation does not have " +
                        "the required permissions to delete the stack.\n" +
                        ex.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0009");
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0010: An unrecognised exception was thrown while deleting a CloudFormation stack.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0010",
                    ex);
            }
            catch (AmazonServiceException ex)
            {
                ex.GetWebExceptionMessage()
                    .Tee(message => logger.Warn("AWS-CLOUDFORMATION-ERROR-0014", message));
                throw ex;
            }
        }
    }
    

    public class CloudFormationParametersFile : ITemplate, ITemplateInputs<Parameter>
    {
        private readonly Func<string> content;
        private readonly Func<string, List<Parameter>> parse;

        public static CloudFormationParametersFile Create(ResolvedTemplatePath path, ICalamariFileSystem fileSystem)
        {
            return new CloudFormationParametersFile(() => fileSystem.ReadFile(path.Value), JsonConvert.DeserializeObject<List<Parameter>>);
        }

        public CloudFormationParametersFile(Func<string> content, Func<string, List<Parameter>> parse)
        {
            this.content = content;
            this.parse = parse;
        }

        public string Content => content();
        public IReadOnlyList<Parameter> Inputs => content().Map(parse);
    }
 
    public class CloudFormationTemplate: ITemplate, ITemplateInputs<Parameter>, ITemplateOutputs<StackFormationNamedOutput>
    {
        private readonly Func<string> content;
        private readonly Func<string, List<StackFormationNamedOutput>> parse;
        private ITemplateInputs<Parameter> parameters;
        private static readonly Regex OutputsRe = new Regex("\"?Outputs\"?\\s*:");
        
        public CloudFormationTemplate(Func<string> content, ITemplateInputs<Parameter> parameters, Func<string, List<StackFormationNamedOutput>> parse)
        {
            this.content = content;
            this.parameters = parameters;
            this.parse = parse;
        }

        public static CloudFormationTemplate Create(ResolvedTemplatePath path, ITemplateInputs<Parameter> parameters, ICalamariFileSystem filesSystem)
        {
            Guard.NotNull(path, "Path must not be null");
            return new CloudFormationTemplate(() => filesSystem.ReadFile(path.Value), parameters, JsonConvert.DeserializeObject<List<StackFormationNamedOutput>> );
        }

        public string Content => content();

        public IReadOnlyList<Parameter> Inputs => parameters.Inputs;
        public bool HasOutputs => Content.Map(OutputsRe.IsMatch);
        public IReadOnlyList<StackFormationNamedOutput> Outputs  => HasOutputs ? parse(Content) : new List<StackFormationNamedOutput>();
    }

    public static class TemplateExtensions
    {
        public static string ApplyVariableSubstitution(this ITemplate template, CalamariVariableDictionary variables)
        {
            return variables.Evaluate(template.Content);
        }
    }

    public class GenerateCloudFormationChangesetNameConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            var name = $"octo-{Guid.NewGuid():N}";
            
            if (string.Compare(deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Generate], "True", StringComparison.OrdinalIgnoreCase) == 0)
            {
                deployment.Variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Name, name);
            }

            Log.SetOutputVariable("ChangesetName", name);
        }
    }

    public class CreateCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly CloudFormationTemplate template;

        public CreateCloudFormationChangeSetConvention(Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            CloudFormationTemplate template
            )
        {
            Guard.NotNull(stackProvider, "Stack provider should not be null");
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.template = template;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            Guard.NotNull(stack, "The stack must be provided to create a change set");
            
            var name = deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Name];
            Guard.NotNullOrWhiteSpace(name, "The changeset name was not specified.");
           
            var status = clientFactory.GetStackStatus(stack, StackStatus.DoesNotExist);

            var request = CreateChangesetRequest(
                status, 
                name,
                template.ApplyVariableSubstitution(deployment.Variables),
                stack, 
                template.Inputs.ToList()
            );

            var result = clientFactory().CreateChangeSet(request)
                .Map(x => new RunningChangeSet(stack, new ChangeSetArn(x.Id)));
            
            clientFactory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, result);
            
            Log.SetOutputVariable("ChangesetId", result.ChangeSet.Value);
            Log.SetOutputVariable("StackId", result.Stack.Value);
            
            deployment.Variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Arn, result.ChangeSet.Value);
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

    public class ExecuteCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, ChangeSetArn> changeSetProvider;
        private readonly StackEventLogger logger;
        private readonly bool waitForComplete;

        public ExecuteCloudFormationChangeSetConvention(
            Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider, 
            Func<RunningDeployment, ChangeSetArn> changeSetProvider,
            StackEventLogger logger,
            bool waitForComplete)
        {
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(changeSetProvider, "Change set provider must nobe null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
            this.logger = logger;
            this.waitForComplete = waitForComplete;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");

            var result = ExecuteChangeset(clientFactory, stack, changeSet);

            if (result.None())
            {
                Log.Info("No changes changes are to be performed.");
                return;
            }

            if (waitForComplete)
            {
                clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, status =>
                {
                    logger.Log(status);
                    logger.LogRollbackError(status, x => clientFactory.GetLastStackEvent(stack, x));
                    return status.ToMaybe().SelectValueOr(x =>
                        (x.ResourceStatus.Value.EndsWith("_COMPLETE") ||
                         x.ResourceStatus.Value.EndsWith("_FAILED")) &&
                        x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);
                });
            }
        }

        private Maybe<RunningChangeSet> ExecuteChangeset(Func<AmazonCloudFormationClient> factory, StackArn stack,
            ChangeSetArn changeSet)
        {
            try
            {
                var changes = factory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, new RunningChangeSet(stack, changeSet));

                if (changes.Status == ChangeSetStatus.FAILED &&
                    string.Compare(changes.StatusReason, "No updates are to be performed.",
                        StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    //We don't need the failed changeset to hang around if there are no changes
                    factory().DeleteChangeSet(new DeleteChangeSetRequest
                    {
                        ChangeSetName = changeSet.Value,
                        StackName = stack.Value
                    });
                    
                    return Maybe<RunningChangeSet>.None;
                }

                factory().ExecuteChangeSet(new ExecuteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });

                return new RunningChangeSet(stack, changeSet).AsSome();
            }
            catch (AmazonCloudFormationException exception)
            {
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

    public class StackOutputsAsVariablesConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly CloudFormationTemplate template;

        public StackOutputsAsVariablesConvention(
            Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            CloudFormationTemplate template)
        {
            Guard.NotNull(clientFactory, "Client factory must not be null");
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(template, "Template must not be null");
            
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.template = template;
        }
        
        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "Deployment must not be null");
            var stack = stackProvider(deployment);
            
            Guard.NotNull(stack, "The provided stack may not be null.");
            
            GetAndPipeOutputVariablesWithRetry(() => clientFactory.DescribeStack(stack).ToMaybe(), 
                deployment.Variables, 
                true, 
                CloudFormationDefaults.RetryCount, 
                CloudFormationDefaults.StatusWaitPeriod);
        }
      
        public (IReadOnlyList<VariableOutput> result, bool success) GetOutputVariables(
            Func<Maybe<Stack>> query)
        {
            Guard.NotNull(query, "Query for stack may not be null");

            List<VariableOutput> ConvertStackOutputs(Stack stack) =>
                stack.Outputs.Select(p => new VariableOutput(p.OutputKey, p.OutputValue)).ToList();

            return query().Select(ConvertStackOutputs)
                .Map(result => (result: result.SomeOr(new List<VariableOutput>()), success: template.HasOutputs || result.Some()));
        }

        public void PipeOutputs(IEnumerable<VariableOutput> outputs, CalamariVariableDictionary variables, string name = "AwsOutputs")
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
                var (result, success) = GetOutputVariables(query);
                if (success || !wait)
                {
                    PipeOutputs(result, variables);
                    break;
                }

                // Wait for a bit for and try again
                Thread.Sleep(waitPeriod);
            }
        }
    }
    
    public class VariableOutput
    {
        public VariableOutput(string name, string value)
        {
            Name = name;
            Value = value;
        }
        
        public string Name { get; }
        public string Value { get; }
    }

    public class RunningChangeSet
        {
            public StackArn Stack { get; }
            public ChangeSetArn ChangeSet { get; }

            public RunningChangeSet(StackArn stack, ChangeSetArn changeSet)
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