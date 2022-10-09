using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Newtonsoft.Json;

namespace Calamari.Aws.Deployment.Conventions
{
    public class DescribeCloudFormationChangeSetConvention : CloudFormationInstallationConventionBase
    {
        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly Func<RunningDeployment, ChangeSetArn> changeSetProvider;

        public DescribeCloudFormationChangeSetConvention(Func<IAmazonCloudFormation> clientFactory,
            StackEventLogger logger,
            Func<RunningDeployment, StackArn> stackProvider,
            Func<RunningDeployment, ChangeSetArn> changeSetProvider) : base(logger)
        {
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.changeSetProvider = changeSetProvider;
        }

        public override void Install(RunningDeployment deployment)
        {
           InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private Task InstallAsync(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            return WithAmazonServiceExceptionHandling(async () => 
                await DescribeChangeset(stack, changeSet, deployment.Variables)
            );
        }
        
        public async Task DescribeChangeset(StackArn stack, ChangeSetArn changeSet, IVariables variables)
        {
            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");
            Guard.NotNull(variables, "The variable dictionary may not be null");

            try
            {
                var response = await clientFactory.DescribeChangeSetAsync(stack, changeSet);
                SetOutputVariable(variables, "ChangeCount", response.Changes.Count.ToString());
                SetOutputVariable(variables, "Changes",
                    JsonConvert.SerializeObject(response.Changes, Formatting.Indented));
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                    "The AWS account used to perform the operation does not have the required permissions to describe the change set.\n" +
                    "Please ensure the current account has permission to perfrom action 'cloudformation:DescribeChangeSet'." +
                    ex.Message + "\n");
            }
            catch (AmazonCloudFormationException ex)
            {
                throw new UnknownException("An unrecognized exception was thrown while describing the CloudFormation change set.", ex);
            }
        }
    }
}