using System;
using FluentValidation;

namespace Sashimi.Server.Contracts.ActionHandlers.Validation
{
    /// <summary>
    /// This interface is used to add validation rules for deployment steps
    /// </summary>
    public interface IDeploymentActionValidator
    {
        /// <summary>
        /// Adds validation rules for deployment steps
        /// </summary>
        /// <param name="validator">The Octopus.Core.Validation.DeploymentActionValidator instance the rules will be added to</param>
        void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator);
    }
}