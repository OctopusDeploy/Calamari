using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Aws.Deployment.Conventions
{
    public class DescribeAwsCloudFormationStackConvention : CloudFormationInstallationConventionBase
    {
        readonly Func<IAmazonCloudFormation> clientFactory;
        readonly Func<RunningDeployment, StackArn> stackProvider;
        readonly Func<List<StackResourceSummary>, List<KeyValuePair<string, string>>> customOutputPropertiesProvider;

        public DescribeAwsCloudFormationStackConvention(
            Func<IAmazonCloudFormation> clientFactory,
            Func<RunningDeployment, StackArn> stackProvider,
            Func<List<StackResourceSummary>, List<KeyValuePair<string, string>>> customOutputPropertiesProvider,
            StackEventLogger logger) : base(logger)
        {
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.customOutputPropertiesProvider = customOutputPropertiesProvider;
        }

        public override void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        Task InstallAsync(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);

            return WithAmazonServiceExceptionHandling(async () =>
                                                          await DescribeStack(stack, deployment.Variables)
                                                     );
        }

        async Task DescribeStack(StackArn stack, IVariables variables)
        {
            Guard.NotNull(stack, "The provided stack identifier or name may not be null");
            Guard.NotNull(variables, "The variable dictionary may not be null");

            try
            {
                var stackResponse = await clientFactory.DescribeStackAsync(stack);
                var resourceSummaries = await clientFactory.ListStackResourcesAsync(stack);

                var outputs = BuildOutputVariables(stackResponse, resourceSummaries, variables);

                Log.Verbose($"OUTPUTS: {JsonConvert.SerializeObject(outputs)}");
                foreach (var output in outputs)
                    Log.SetOutputVariable(output.Key, output.Value, variables);
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                                              "The AWS account used to perform the operation does not have the required permissions to describe the stack.\n" + "Please ensure the current account has permission to perform action 'cloudformation:DescribeStacks' and 'cloudformation:DescribeStackResources'." + ex.Message + "\n");
            }
            catch (AmazonCloudFormationException ex)
            {
                throw new UnknownException("An unrecognized exception was thrown while describing the CloudFormation stack.", ex);
            }
        }

        Dictionary<string, string> BuildOutputVariables(Stack stack, List<StackResourceSummary> stackResourceSummaries, IVariables variables)
        {
            var outputs = new Dictionary<string, string>
            {
                ["StackName"] = stack.StackName,
                ["StackId"] = stack.StackId,
                ["Region"] = variables.Get("Octopus.Action.Aws.Region")?.Trim()
            };

            var customOutputProperties = customOutputPropertiesProvider(stackResourceSummaries);
            foreach (var kvp in customOutputProperties)
                outputs.Add(kvp.Key, kvp.Value);

            return outputs;
        }
    }
}