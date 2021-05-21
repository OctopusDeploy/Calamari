using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.GCPScripting
{
    class GoogleCloudActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public GoogleCloudActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.GoogleCloud.ActionTypeName,
                () =>
                {
                    RuleFor(a => a.Properties)
                        .MustHaveProperty(SpecialVariables.Action.GoogleCloud.AccountId, "Please select an Account or provide a variable expression for the Account ID to use.");
                });
        }
    }
}