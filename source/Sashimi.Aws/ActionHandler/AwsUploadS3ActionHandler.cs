using Calamari.Aws;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.Aws.ActionHandler
{
    public class AwsUploadS3ActionHandler : IActionHandler
    {
        public string Id => AwsActionTypes.UploadS3;
        public string Name => "Upload a package to an AWS S3 bucket";
        public string Description => "Upload a package or package contents to an AWS S3 bucket.";
        public string Keywords => string.Empty;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => true;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, AwsConstants.AwsActionHandlerCategory, ActionHandlerCategory.Package };

        public IActionHandlerResult Execute(IActionHandlerContext context)
            => context.CalamariCommand(AwsConstants.CalamariAws, KnownAwsCalamariCommands.Commands.UploadAwsS3)
                .WithStagedPackageArgument()
                .Execute();
    }
}