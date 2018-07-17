using System;
using Amazon.CloudFormation;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
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

            Guard.NotNull(stack, "The provided stack identifer or name may not be null");
            Guard.NotNull(changeSet, "The provided change set identifier or name may not be null");
            
            WithAmazonServiceExceptionHandling(() =>
            {
                var response = clientFactory.DescribeChangeSet(stack, changeSet);
                Log.SetOutputVariable("ChangeCount", response.Changes.Count.ToString(), deployment.Variables);
                Log.SetOutputVariable("Changes", JsonConvert.SerializeObject(response.Changes), deployment.Variables);
            });
        }
    }
}