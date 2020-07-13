using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.Aws.Validation
{
    class AwsApplyChangeSetCloudFormationValidator : AwsDeploymentValidatorBase
    {
        public AwsApplyChangeSetCloudFormationValidator() : base(AwsActionTypes.ApplyCloudFormationChangeset)
        {
        }

        public override void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            base.AddDeploymentValidationRule(validator);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.StackName, "Please provide the CloudFormation stack name.")
                .When(ThisAction);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Arn, "Please provide the Change Set name or identifier")
                .When(ThisAction);
        }
    }
}