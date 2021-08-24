using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Common.Util;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationS3Template : ICloudFormationRequestBuilder
    {
        ITemplateInputs<Parameter> parameters;

        public CloudFormationS3Template(ITemplateInputs<Parameter> parameters,
                                        string templateS3Url,
                                        string templateParameterS3Url)
        {
            this.parameters = parameters;
            TemplateS3Url = templateS3Url;
            TemplateParameterS3Url = templateParameterS3Url;
        }

        public static CloudFormationS3Template Create(ITemplateInputs<Parameter> parameters,
                                                      string templateS3Url,
                                                      string templateParameterS3Url)
        {
            return new CloudFormationS3Template(parameters,
                                                templateS3Url,
                                                templateParameterS3Url);
        }

        string TemplateS3Url { get; }
        string TemplateParameterS3Url { get; }

        public CreateStackRequest BuildCreateStackRequest(string stackName, List<string> capabilities, bool disableRollback, string roleArn, List<Tag> tags)
        {
            return new CreateStackRequest
            {
                StackName = stackName,
                TemplateURL = TemplateS3Url,
                Parameters = null,
                Capabilities = capabilities,
                DisableRollback = disableRollback,
                RoleARN = roleArn,
                Tags = tags
            };
        }

        public UpdateStackRequest BuildUpdateStackRequest(string stackName, List<string> capabilities, string roleArn, List<Tag> tags)
        {
            return new UpdateStackRequest
            {
                StackName = stackName,
                TemplateURL = TemplateS3Url,
                Parameters = null,
                Capabilities = capabilities,
                RoleARN = roleArn,
                Tags = tags
            };
        }

        public CreateChangeSetRequest BuildChangesetRequest(StackStatus status,
                                                            string changesetName,
                                                            StackArn stack,
                                                            string roleArn,
                                                            List<string> capabilities)
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateURL = TemplateS3Url,
                Parameters = Inputs.ToList(),
                ChangeSetName = changesetName,
                ChangeSetType = status == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                Capabilities = capabilities,
                RoleARN = roleArn
            };
        }

        public IEnumerable<Parameter> Inputs { get; }
    }

    public class CreateChangesetRequest : CreateChangeSetRequest
    {
        public CreateChangesetRequest(StackStatus status,
                                      object name,
                                      StackArn stack,
                                      object invoke,
                                      object template,
                                      object capabilities)
        {
            throw new NotImplementedException();
        }
    }
}