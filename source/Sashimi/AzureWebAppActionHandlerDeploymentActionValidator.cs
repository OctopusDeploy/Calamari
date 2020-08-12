using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppActionHandlerDeploymentActionValidator : IDeploymentActionValidator
    {
        public virtual void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.Include(new AzureWebAppActionHandlerValidator());
        }
    }
}