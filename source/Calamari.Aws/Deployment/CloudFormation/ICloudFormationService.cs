using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;

namespace Calamari.Aws.Deployment.CloudFormation
{
    public interface ICloudFormationService
    {
        Task DeleteByStackArn(StackArn stackArn, bool waitForCompletion);

        Task ExecuteChangeSet(StackArn stackArn, ChangeSetArn changeSetArn, bool waitForCompletion);

        Task<IReadOnlyCollection<VariableOutput>> GetOutputVariablesByStackArn(StackArn stackArn);

        Task<RunningChangeSet> CreateChangeSet(string changeSetName, CloudFormationTemplate cloudFormationTemplate, StackArn stackArn,
            string roleArn, IReadOnlyCollection<string> iamCapabilities);

        Task<IReadOnlyCollection<Change>> GetChangeSet(StackArn stackArn, ChangeSetArn changeSetArn);

        Task<string> Deploy(CloudFormationTemplate cloudFormationTemplate, StackArn stackArn, string roleArn,
            IReadOnlyCollection<string> iamCapabilities, bool isRollbackDisabled, bool waitForCompletion);
    }
}