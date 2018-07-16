using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Util;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
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
}