using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Tag = Amazon.CloudFormation.Model.Tag;

namespace Calamari.Aws.Integration.Ecs.CloudFormation
{
    // Adapts a prebuilt CF template body + parameters to ICloudFormationRequestBuilder so the
    // shared DeployAwsCloudFormationConvention can drive the deploy. The ECS step receives the
    // template and parameters from the server-side SPF mapper; this class just wraps them.
    public class EcsCloudFormationRequestBuilder : ICloudFormationRequestBuilder
    {
        static readonly List<string> RequiredCapabilities = new() { "CAPABILITY_NAMED_IAM" };

        readonly string templateBody;
        readonly List<Parameter> parameters;
        readonly string stackName;
        readonly List<Tag> tags;
        readonly string roleArn;

        public EcsCloudFormationRequestBuilder(string templateBody, IEnumerable<Parameter> parameters, string stackName, List<Tag> tags, string roleArn)
        {
            this.templateBody = templateBody;
            this.parameters = parameters?.ToList() ?? [];
            this.stackName = stackName;
            this.tags = tags;
            this.roleArn = roleArn;
        }

        public IEnumerable<Parameter> Inputs => parameters;

        public CreateStackRequest BuildCreateStackRequest() => new()
        {
            StackName = stackName,
            TemplateBody = templateBody,
            Parameters = parameters,
            Tags = tags,
            Capabilities = RequiredCapabilities,
            RoleARN = roleArn
        };

        public UpdateStackRequest BuildUpdateStackRequest() => new()
        {
            StackName = stackName,
            TemplateBody = templateBody,
            Parameters = parameters,
            Tags = tags,
            Capabilities = RequiredCapabilities,
            RoleARN = roleArn
        };

        public Task<CreateChangeSetRequest> BuildChangesetRequest() =>
            throw new NotSupportedException("The ECS deploy step does not use CloudFormation change sets.");
    }
}
