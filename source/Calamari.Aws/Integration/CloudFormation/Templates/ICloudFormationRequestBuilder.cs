using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Aws.Deployment.Conventions;
using Amazon.CloudFormation.Model;
using Calamari.Common.Util;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public interface ICloudFormationRequestBuilder : ITemplateInputs<Parameter>
    {
        CreateStackRequest BuildCreateStackRequest();

        UpdateStackRequest BuildUpdateStackRequest();

        Task<CreateChangeSetRequest> BuildChangesetRequest();
    }
}