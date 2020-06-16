using FluentValidation;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.Aws.Validation
{
    class AwsRunScriptDeploymentValidator : AwsDeploymentValidatorBase
    {
        public AwsRunScriptDeploymentValidator() : base(AwsActionTypes.RunScript)
        {
        }

        public override void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            base.AddDeploymentValidationRule(validator);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(KnownVariables.Action.Script.ScriptBody, "Please provide the script body to run.")
                .When(ThisAction)
                .When(a => !ScriptIsFromPackage(a.Properties));

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(KnownVariables.Action.Script.ScriptFileName, "Please provide an AWS script file name.")
                .When(ThisAction)
                .When(a => ScriptIsFromPackage(a.Properties));
        }
    }
}