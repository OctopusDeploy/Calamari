using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.GCPScripting
{
    class AzurePowerShellActionHandlerDeploymentActionValidator : IDeploymentActionValidator
    {
        public virtual void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.Include(new GoogleCloudActionHandlerValidator());
        }
    }
}