using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.GoogleCloud.Scripting
{
    class GoogleCloudActionHandlerDeploymentActionValidator : IDeploymentActionValidator
    {
        public virtual void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.Include(new GoogleCloudActionHandlerValidator());
        }
    }
}