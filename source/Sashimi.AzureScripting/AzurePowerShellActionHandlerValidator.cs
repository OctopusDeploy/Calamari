using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureScripting
{
    class AzurePowerShellActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public AzurePowerShellActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.Azure.ActionTypeName,
                 () =>
                 {
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.Azure.AccountId, "Please select an Account or provide a variable expression for the Account ID to use.");
                 });
        }
    }
}