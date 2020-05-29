using Calamari.Aws;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.Aws.ActionHandler
{
    /// <summary>
    /// The action handler that prepares a Calamari script execution with
    /// the path set to include the AWS CLI and having AWS credentials
    /// set in the common environment variable paths. It then goes on to
    /// deploy a cloud formation template.
    /// </summary>
    public class AwsApplyCloudFormationChangeSetActionHandler : IActionHandler
    {
        public string Id => AwsActionTypes.ApplyCloudFormationChangeset;
        public string Name => "Apply an AWS CloudFormation Change Set";
        public string Description => "Applies an existing AWS CloudFormation Change Set";
        public string Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, AwsConstants.AwsActionHandlerCategory };

        public IActionHandlerResult Execute(IActionHandlerContext context)
        {
            var builder = context.CalamariCommand(AwsConstants.CalamariAws, KnownAwsCalamariCommands.Commands.ApplyAwsCloudFormationChangeSet);
            return builder.Execute();
        }
    }
}