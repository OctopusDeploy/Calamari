using System.Threading.Tasks;
using Calamari.Aws.Deployment.CloudFormation;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Util;
using Calamari.Commands.Support;

namespace Calamari.Aws
{
    [Command(KnownAwsCalamariCommands.Commands.DeleteAwsCloudFormation, Description = "Destroy an existing AWS CloudFormation stack")]
    public class DeleteCloudFormationCommand : AwsCommand
    {
        readonly ICloudFormationService cloudFormationService;

        public DeleteCloudFormationCommand(
            ILog log,
            IVariables variables,
            IAmazonClientFactory amazonClientFactory,
            ICloudFormationService cloudFormationService)
            : base(log, variables, amazonClientFactory)
        {
            this.cloudFormationService = cloudFormationService;
        }

        protected override Task ExecuteCoreAsync()
        {
            var stackArn = new StackArn(variables.Get(SpecialVariableNames.Aws.CloudFormation.StackName));
            var waitForCompletion = variables.GetFlag(SpecialVariableNames.Action.WaitForCompletion);

            return cloudFormationService.DeleteByStackArn(stackArn, waitForCompletion);
        }
    }
}