﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class CloudFormationOutputsAsVariablesConvention : CloudFormationInstallationConventionBase
    {
        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        readonly int retryCount = 3;

        public CloudFormationOutputsAsVariablesConvention(
            Func<IAmazonCloudFormation> clientFactory,
            StackEventLogger stackEventLogger,
            Func<RunningDeployment, StackArn> stackProvider, ILog log): base(stackEventLogger, log)
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

        Task InstallAsync(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "Deployment must not be null");
            var stack = stackProvider(deployment);

            Guard.NotNull(stack, "The provided stack may not be null.");

            return GetAndPipeOutputVariablesWithRetry(() =>
                    WithAmazonServiceExceptionHandling(async () => (await QueryStackAsync(clientFactory, stack)).ToMaybe()),
                deployment.Variables,
                true,
                PollPeriod(deployment));
        }

        async Task<(IReadOnlyList<VariableOutput> result, bool success)> GetOutputVariables(
            Func<Task<Maybe<Stack>>> query)
        {
            Guard.NotNull(query, "Query for stack may not be null");

            List<VariableOutput> ConvertStackOutputs(Stack stack) =>
                stack.Outputs.Select(p => new VariableOutput(p.OutputKey, p.OutputValue)).ToList();

            return (await query()).Select(ConvertStackOutputs)
                .Map(result => (result: result.SomeOr(new List<VariableOutput>()), success: true));
        }

        void PipeOutputs(IEnumerable<VariableOutput> outputs, IVariables variables, string name = "AwsOutputs")
        {
            Guard.NotNull(variables, "Variables may not be null");

            foreach (var output in outputs)
            {
                SetOutputVariable(variables, output.Name, output.Value );
            }
        }

        async Task GetAndPipeOutputVariablesWithRetry(Func<Task<Maybe<Stack>>> query, IVariables variables, bool wait, TimeSpan pollPeriod)
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
                await Task.Delay(pollPeriod);
            }
        }
    }
}