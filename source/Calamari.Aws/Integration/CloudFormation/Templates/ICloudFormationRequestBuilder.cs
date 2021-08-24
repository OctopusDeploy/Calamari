using System.Collections.Generic;
using Calamari.Aws.Deployment.Conventions;
using Amazon.CloudFormation.Model;
using Calamari.Common.Util;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public interface ICloudFormationRequestBuilder : ITemplateInputs<Parameter>//, ITemplateOutputs<StackFormationNamedOutput>
    {
        CreateStackRequest BuildCreateStackRequest(string stackName,
                                                   List<string> capabilities,
                                                   bool disableRollback,
                                                   string roleArn,
                                                   List<Tag> tags);

        UpdateStackRequest BuildUpdateStackRequest(string stackName,
                                                   List<string> capabilities,
                                                   string roleArn,
                                                   List<Tag> tags);

        CreateChangeSetRequest BuildChangesetRequest(StackStatus status,
                                                     string changesetName,
                                                     StackArn stack,
                                                     string roleArn,
                                                     List<string> capabilities);
    }
}