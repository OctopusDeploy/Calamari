using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Util;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class CreateCloudFormationChangeSetConvention : CloudFormationInstallationConventionBase
    {
        private readonly Func<IAmazonCloudFormation> clientFactory;
        private readonly Func<RunningDeployment, StackArn> stackProvider;
        private readonly CloudFormationTemplate template;
        private readonly Func<RunningDeployment, string> roleArnProvider;

        public CreateCloudFormationChangeSetConvention(Func<IAmazonCloudFormation> clientFactory,
            StackEventLogger logger,
            Func<RunningDeployment, StackArn> stackProvider,
            Func<RunningDeployment, string> roleArnProvider,
            CloudFormationTemplate template
        ): base(logger)
        {
            Guard.NotNull(stackProvider, "Stack provider should not be null");
            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.roleArnProvider = roleArnProvider;
            this.template = template;
        }

        public override void Install(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            Guard.NotNull(stack, "The stack must be provided to create a change set");
            
            var name = deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Name];
            Guard.NotNullOrWhiteSpace(name, "The changeset name was not specified.");

            try
            {
                var status = clientFactory.GetStackStatus(stack, StackStatus.DoesNotExist);

                var request = CreateChangesetRequest(
                    status,
                    name,
                    template.ApplyVariableSubstitution(deployment.Variables),
                    stack,
                    template.Inputs.ToList(),
                    roleArnProvider?.Invoke(deployment)
                );

                var result = clientFactory().CreateChangeSet(request)
                    .Map(x => new RunningChangeSet(stack, new ChangeSetArn(x.Id)));

                WithAmazonServiceExceptionHandling(() =>
                    clientFactory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, result));

                Log.SetOutputVariable("ChangesetId", result.ChangeSet.Value);
                Log.SetOutputVariable("StackId", result.Stack.Value);

                deployment.Variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Arn, result.ChangeSet.Value);
            }
            catch (AmazonServiceException exception)
            {
                HandleAmazonServiceException(exception);
                throw;
            }
        }

        public CreateChangeSetRequest CreateChangesetRequest(StackStatus status, string changesetName, string template, StackArn stack,  List<Parameter> parameters, string roleArn)
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateBody = template,
                Parameters = parameters,
                ChangeSetName = changesetName,
                ChangeSetType = status == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                RoleARN = roleArn 
            };
        }
    }
}