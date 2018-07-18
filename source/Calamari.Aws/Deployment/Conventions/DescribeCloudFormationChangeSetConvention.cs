using System;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
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
            var stack = stackProvider(deployment);
            var changeSet = changeSetProvider(deployment);

            WithAmazonServiceExceptionHandling(() => DescribeChangeset(stack, changeSet, deployment.Variables));
        }
        
        public void DescribeChangeset(StackArn stack, ChangeSetArn changeSet, CalamariVariableDictionary variables)
        {
            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");
            Guard.NotNull(variables, "The variable dictionary may not be null");

            try
            {
                var response = clientFactory.DescribeChangeSet(stack, changeSet);
                SetOutputVariable(variables, "ChangeCount", response.Changes.Count.ToString());
                SetOutputVariable(variables, "Changes", JsonConvert.SerializeObject(response.Changes, Formatting.Indented));
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        @"AWS-CLOUDFORMATION-ERROR-0015: The AWS account used to perform the operation does not have " +
                        "the required permissions to describe the change set.\n" +
                        ex.Message + "\n" +
                        "For more information visit the [octopus docs](https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0015)");
                }
                
                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0016: An unrecognised exception was thrown while describing the CloudFormation change set.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0016",
                    ex);
            }
        }
    }
}