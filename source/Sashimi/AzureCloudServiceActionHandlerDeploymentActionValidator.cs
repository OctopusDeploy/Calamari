using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServiceActionHandlerDeploymentActionValidator : IDeploymentActionValidator
    {
        public virtual void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.Include(new AzureCloudServiceActionHandlerValidator());
        }
    }
}