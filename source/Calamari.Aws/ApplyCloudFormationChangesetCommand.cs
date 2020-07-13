using System.Threading.Tasks;
using Calamari.Aws.Deployment.CloudFormation;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Util;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws
{
    [Command(KnownAwsCalamariCommands.Commands.ApplyAwsCloudFormationChangeSet, Description = "Apply an existing AWS CloudFormation changeset")]
    public class ApplyCloudFormationChangeSetCommand : AwsCommand
    {
        readonly ICloudFormationService cloudFormationService;

        public ApplyCloudFormationChangeSetCommand(
            ILog log,
            IVariables variables,
            IAmazonClientFactory amazonClientFactory,
            ICloudFormationService cloudFormationService)
            : base(log, variables, amazonClientFactory)
        {
            this.cloudFormationService = cloudFormationService;
        }

        protected override async Task ExecuteCoreAsync()
        {
            var stackArn = new StackArn(variables.Get(SpecialVariableNames.Aws.CloudFormation.StackName));
            var changeSetArn = new ChangeSetArn(variables.Get(SpecialVariableNames.Aws.CloudFormation.ChangeSets.Arn));
            var waitForCompletion = variables.GetFlag(SpecialVariableNames.Action.WaitForCompletion, true);

            await cloudFormationService.ExecuteChangeSet(stackArn, changeSetArn, waitForCompletion);

            SetOutputVariables(await cloudFormationService.GetOutputVariablesByStackArn(stackArn));
        }
    }
}