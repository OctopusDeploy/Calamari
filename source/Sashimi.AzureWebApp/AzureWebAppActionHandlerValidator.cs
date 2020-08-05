using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public AzureWebAppActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.Azure.WebAppActionTypeName,
                 () =>
                 {
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.Azure.WebAppName, "Please select a WebApp or provide a variable expression for the WebApp Name to target.")
                         .When(a => a.Properties.ContainsKey(SpecialVariables.Action.Azure.AccountId));
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.Azure.AccountId, "Please select an Account or provide a variable expression for the Account ID to use.")
                         .When(a => a.Properties.ContainsKey(SpecialVariables.Action.Azure.IsLegacyMode));
                 });
        }
    }
}