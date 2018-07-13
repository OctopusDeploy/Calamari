
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
            
            if (string.Compare(deployment.Variables[AwsSpecialVariables.Changesets.Generate], "True", StringComparison.OrdinalIgnoreCase) == 0)
            {
                deployment.Variables.Set(AwsSpecialVariables.Changesets.Name, name);
            }

            Log.SetOutputVariable("ChangesetName", name);
        }
    }

    public class CreateCloudFormationChangeSetConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly CloudFormationExecutionContext context;
        private readonly Func<RunningDeployment, StackArn> stackProvider;

        public CreateCloudFormationChangeSetConvention(Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            CloudFormationExecutionContext context
            )
        {
            Guard.NotNull(stackProvider, "Stack provider should not be null");
            Guard.NotNull(context, "Execution context may not be null");
            this.clientFactory = clientFactory;
            this.context = context;
            this.stackProvider = stackProvider;
        }

        public void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            Guard.NotNull(stack, "The stack must be provided to create a change set");
            
            var name = deployment.Variables[AwsSpecialVariables.Changesets.Name];
            Guard.NotNullOrWhiteSpace(name, "The changeset name was not specified.");
           
            var status = clientFactory.GetStackStatus(stack, StackStatus.DoesNotExist);

            var request = CreateChangesetRequest(
                status, 
                name, 
                context.Template.ApplyVariableSubstitution(deployment.Variables), 
                stack, 
                context.Template.Inputs.ToList()
            );

            var result = clientFactory().CreateChangeSet(request)
                .Map(x => new RunningChangeset(stack, new ChangeSetArn(x.Id)));
            
            clientFactory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, result);
            
            Log.SetOutputVariable("ChangesetId", result.ChangeSet.Value);
            Log.SetOutputVariable("StackId", result.Stack.Value);
            
            deployment.Variables.Set(AwsSpecialVariables.Changesets.Changeset, result.ChangeSet.Value);
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
        private readonly CloudFormationExecutionContext context;
        private readonly ILog logger;

        public ExecuteCloudFormationChangeSetConvention(
            Func<AmazonCloudFormationClient> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider, 
            Func<RunningDeployment, ChangeSetArn> changeSetProvider,
            CloudFormationExecutionContext context,
            ILog logger)
        {
            Guard.NotNull(stackProvider, "Stack provider must not be null");
            Guard.NotNull(changeSetProvider, "Change set provider must nobe null");
            Guard.NotNull(clientFactory, "Client factory should not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
            this.context = context;
            this.logger = logger;
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
            
            //If Wait for completion
            var stackLogger = new StackEventLogger(logger, x => clientFactory.GetLastStackEvent(stack, x));
            clientFactory.WaitForStackToComplete(CloudFormationDefaults.StatusWaitPeriod, stack, status =>
            {
                stackLogger.Log(status);
                stackLogger.LogRollbackError(status);
                return status.ToMaybe().SelectValueOr(x => 
                    (x.ResourceStatus.Value.EndsWith("_COMPLETE") ||
                     x.ResourceStatus.Value.EndsWith("_FAILED")) &&
                     x.ResourceType.Equals("AWS::CloudFormation::Stack"), true);
            });
            
            context.GetAndPipeOutputVariablesWithRetry(() => clientFactory.DescribeStack(stack).ToMaybe(), 
                deployment.Variables, 
                true, 
                CloudFormationDefaults.RetryCount, 
                CloudFormationDefaults.StatusWaitPeriod);
        }

        private Maybe<RunningChangeset> ExecuteChangeset(Func<AmazonCloudFormationClient> factory, StackArn stack,
            ChangeSetArn changeSet)
        {
            try
            {
                var changes = factory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, new RunningChangeset(stack, changeSet));

                if (changes.Changes.Count == 0)
                    return Maybe<RunningChangeset>.None;

                factory().ExecuteChangeSet(new ExecuteChangeSetRequest
                {
                    ChangeSetName = changeSet.Value,
                    StackName = stack.Value
                });

                return new RunningChangeset(stack, changeSet).AsSome();
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
    
    public class CloudFormationExecutionContext
    {
        public CloudFormationTemplate Template { get; }
        
        public CloudFormationExecutionContext(
            CloudFormationTemplate template)
        {
            Template = template;
        }
                
        public (IReadOnlyList<VariableOutput> result, bool success) GetOutputVariables(
            Func<Maybe<Stack>> query)
        {
            Guard.NotNull(query, "Query for stack may not be null");

            List<VariableOutput> ConvertStackOutputs(Stack stack) =>
                stack.Outputs.Select(p => new VariableOutput(p.OutputKey, p.OutputValue)).ToList();

            return query().Select(ConvertStackOutputs)
                .Map(result => (result: result.SomeOr(new List<VariableOutput>()), success: Template.HasOutputs || result.Some()));
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