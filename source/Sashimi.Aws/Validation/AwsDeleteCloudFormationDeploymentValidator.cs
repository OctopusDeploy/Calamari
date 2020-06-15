using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.Aws.Validation
{
    public class AwsDeleteCloudFormationDeploymentValidator : AwsDeploymentValidatorBase
    {
        public AwsDeleteCloudFormationDeploymentValidator() : base(AwsActionTypes.DeleteCloudFormation)
        {
        }

        public override void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            base.AddDeploymentValidationRule(validator);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.StackName, "Please provide the CloudFormation stack name.")
                .When(ThisAction);
        }
    }
}