using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation.Model;

namespace Calamari.Aws.Integration.CloudFormation.Templates;

// Wraps a CloudFormation template body supplied inline (e.g. from an Octopus variable) as an
// ICloudFormationRequestBuilder. CloudFormationTemplate expects a file path and
// CloudFormationS3Template expects an S3 URL; this fills the inline-body gap.
// Does not support change sets.
public class InlineCloudFormationTemplate : ICloudFormationRequestBuilder
{
    readonly string templateBody;
    readonly List<Parameter> parameters;
    readonly string stackName;
    readonly List<Tag> tags;
    readonly List<string> capabilities;
    readonly string roleArn;

    public InlineCloudFormationTemplate(
        string templateBody,
        IEnumerable<Parameter> parameters,
        string stackName,
        List<Tag> tags,
        List<string> capabilities,
        string roleArn)
    {
        this.templateBody = templateBody;
        this.parameters = parameters?.ToList() ?? [];
        this.stackName = stackName;
        this.tags = tags;
        this.capabilities = capabilities;
        this.roleArn = roleArn;
    }

    public IEnumerable<Parameter> Inputs => parameters;

    public CreateStackRequest BuildCreateStackRequest() => new()
    {
        StackName = stackName,
        TemplateBody = templateBody,
        Parameters = parameters,
        Tags = tags,
        Capabilities = capabilities,
        RoleARN = roleArn
    };

    public UpdateStackRequest BuildUpdateStackRequest() => new()
    {
        StackName = stackName,
        TemplateBody = templateBody,
        Parameters = parameters,
        Tags = tags,
        Capabilities = capabilities,
        RoleARN = roleArn
    };

    public Task<CreateChangeSetRequest> BuildChangesetRequest() =>
        throw new NotSupportedException();
}
