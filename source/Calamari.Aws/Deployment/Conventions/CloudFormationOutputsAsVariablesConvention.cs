using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class CloudFormationOutputsAsVariablesConvention : CloudFormationInstallationConventionBase
    {
        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;

        public CloudFormationOutputsAsVariablesConvention(
            Func<IAmazonCloudFormation> clientFactory,
            StackEventLogger logger,
            Func<RunningDeployment, StackArn> stackProvider): base(logger)
        {
            Guard.NotNull(clientFactory, "Client factory must not be null");
            Guard.NotNull(stackProvider, "Stack provider must not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
        }

        public override void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private Task InstallAsync(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "Deployment must not be null");
            var stack = stackProvider(deployment);

            Guard.NotNull(stack, "The provided stack may not be null.");

            return GetAndPipeOutputVariablesWithRetry(() =>
                    WithAmazonServiceExceptionHandling(async () => (await QueryStackAsync(clientFactory, stack)).ToMaybe()),
                deployment.Variables,
                true,
                CloudFormationDefaults.RetryCount,
                CloudFormationDefaults.StatusWaitPeriod);
        }

        public async Task<(IReadOnlyList<VariableOutput> result, bool success)> GetOutputVariables(
            Func<Task<Maybe<Stack>>> query)
        {
            Guard.NotNull(query, "Query for stack may not be null");

            List<VariableOutput> ConvertStackOutputs(Stack stack) =>
                stack.Outputs.Select(p => new VariableOutput(p.OutputKey, p.OutputValue)).ToList();

            return (await query()).Select(ConvertStackOutputs)
                .Map(result => (result: result.SomeOr(new List<VariableOutput>()), success: true));
        }

        public void PipeOutputs(IEnumerable<VariableOutput> outputs, IVariables variables, string name = "AwsOutputs")
        {
            Guard.NotNull(variables, "Variables may not be null");

            foreach (var output in outputs)
            {
                SetOutputVariable(variables, output.Name, output.Value );
            }
        }

        public async Task GetAndPipeOutputVariablesWithRetry(Func<Task<Maybe<Stack>>> query, IVariables variables, bool wait, int retryCount, TimeSpan waitPeriod)
        {
            for (var retry = 0; retry < retryCount; ++retry)
            {
                var (result, success) = await GetOutputVariables(query);
                if (success || !wait)
                {
                    PipeOutputs(result, variables);
                    break;
                }

                // Wait for a bit for and try again
                await Task.Delay(waitPeriod);
            }
        }
    }
}