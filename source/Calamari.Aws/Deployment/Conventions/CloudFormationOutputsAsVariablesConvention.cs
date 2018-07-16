using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class CloudFormationOutputsAsVariablesConvention : IInstallConvention
    {
        private readonly Func<AmazonCloudFormationClient> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly CloudFormationTemplate template;

        public CloudFormationOutputsAsVariablesConvention(
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
                .Map(result => (result: MaybeExtentions.SomeOr(result, new List<VariableOutput>()), success: template.HasOutputs || MaybeExtentions.Some<List<VariableOutput>>(result)));
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
}