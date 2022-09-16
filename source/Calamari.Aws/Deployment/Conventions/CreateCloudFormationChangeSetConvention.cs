using System;
using System.Collections.Generic;
using System.Linq;
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
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class CreateCloudFormationChangeSetConvention : CloudFormationInstallationConventionBase
    {
        readonly Func<IAmazonCloudFormation> clientFactory;
        readonly Func<RunningDeployment, StackArn> stackProvider;
        readonly Func<ICloudFormationRequestBuilder> templateFactory;

        public CreateCloudFormationChangeSetConvention(Func<IAmazonCloudFormation> clientFactory,
                                                       StackEventLogger logger,
                                                       Func<RunningDeployment, StackArn> stackProvider,
                                                       Func<ICloudFormationRequestBuilder> templateFactory
        ) : base(logger)
        {
            Guard.NotNull(stackProvider, "Stack provider should not be null");
            Guard.NotNull(clientFactory, "Client factory should not be null");
            Guard.NotNull(templateFactory, "Template factory should not be null");

            this.clientFactory = clientFactory;
            this.stackProvider = stackProvider;
            this.templateFactory = templateFactory;
        }

        public override void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private async Task InstallAsync(RunningDeployment deployment)
        {
            var stack = stackProvider(deployment);
            Guard.NotNull(stack, "The provided stack may not be null");

            var name = deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Name];
            Guard.NotNullOrWhiteSpace(name, "The changeset name must be provided.");

            var template = templateFactory();
            Guard.NotNull(template, "CloudFormation template should not be null.");

            try
            {
                var changeset = await CreateChangeSet(await template.BuildChangesetRequest());
                await WaitForChangesetCompletion(changeset);
                ApplyVariables(deployment.Variables)(changeset);
            }
            catch (AmazonServiceException exception)
            {
                LogAmazonServiceException(exception);
                throw;
            }
        }

        private Task WaitForChangesetCompletion(RunningChangeSet result)
        {
            return clientFactory.WaitForChangeSetCompletion(CloudFormationDefaults.StatusWaitPeriod, result);
        }

        private Action<RunningChangeSet> ApplyVariables(IVariables variables)
        {
            return result =>
                   {
                       SetOutputVariable(variables, "ChangesetId", result.ChangeSet.Value);
                       SetOutputVariable(variables, "StackId", result.Stack.Value);
                       variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Arn, result.ChangeSet.Value);
                   };
        }

        private async Task<RunningChangeSet> CreateChangeSet(CreateChangeSetRequest request)
        {
            try
            {
                return (await clientFactory.CreateChangeSetAsync(request))
                    .Map(x => new RunningChangeSet(new StackArn(x.StackId), new ChangeSetArn(x.Id)));
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "AccessDenied")
            {
                throw new PermissionException(
                                              @"The AWS account used to perform the operation does not have the required permissions to create the change set.\n" + "Please ensure the current user has the cloudformation:CreateChangeSet permission.\n" + ex.Message + "\n",
                                              ex);
            }
        }
    }
}