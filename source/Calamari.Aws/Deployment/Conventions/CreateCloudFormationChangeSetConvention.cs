using System;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Deployment;
using Calamari.Integration.Processes;
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
            Guard.NotNull(stack, "The provided stack may not be null");
            
            var name = deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Name];
            Guard.NotNullOrWhiteSpace(name, "The changeset name must be provided.");

            try
            {
                var status = clientFactory.StackExists(stack, StackStatus.DoesNotExist);

                CreateChangesetRequest(
                    deployment.Variables,
                    status,
                    name,
                    stack,
                    roleArnProvider?.Invoke(deployment)
                )
                .Map(CreateChangeSet)
                .Tee(WaitForChangesetCompletion)
                .Tee(ApplyVariables(deployment.Variables));
            }
            catch (AmazonServiceException exception)
            {
                HandleAmazonServiceException(exception);
                throw;
            }
        }

        private void WaitForChangesetCompletion(RunningChangeSet result)
        {
            clientFactory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, result);
        }

        private Action<RunningChangeSet> ApplyVariables(CalamariVariableDictionary variables)
        {
            return result =>
            {
                SetOutputVariable(variables, "ChangesetId", result.ChangeSet.Value);
                SetOutputVariable(variables, "StackId", result.Stack.Value);
                variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Arn, result.ChangeSet.Value);
            };
        }

        private CreateChangeSetRequest CreateChangesetRequest(CalamariVariableDictionary variables, StackStatus status, string changesetName, StackArn stack, string roleArn)
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateBody = template.ApplyVariableSubstitution(variables),
                Parameters = template.Inputs.ToList(),
                ChangeSetName = changesetName,
                ChangeSetType = status == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                RoleARN = roleArn 
            };
        }

        private RunningChangeSet CreateChangeSet(CreateChangeSetRequest request)
        {
            try
            {
                return clientFactory.CreateChangeSet(request)
                    .Map(x => new RunningChangeSet(new StackArn(x.StackId), new ChangeSetArn(x.Id)));
            }
            catch (AmazonCloudFormationException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        @"AWS-CLOUDFORMATION-ERROR-0017: The AWS account used to perform the operation does not have " +
                        "the required permissions to create the change set.\n" +
                        ex.Message + "\n" +
                        "For more information visit the [octopus docs](https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0017)");
                }
                
                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0018: An unrecognised exception was thrown while creating the CloudFormation change set.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0018",
                    ex);
            }
        }
    }
}