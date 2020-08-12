using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureResourceGroup
{
    class AzureResourceGroupActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public AzureResourceGroupActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.AzureResourceGroup.ResourceGroupActionTypeName,
                 () =>
                 {
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.Azure.ResourceGroupName, "Please provide a resource group name.")
                         .When(a => a.ActionType == SpecialVariables.Action.AzureResourceGroup.ResourceGroupActionTypeName);
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.AzureResourceGroup.ResourceGroupDeploymentMode, "Please provide a deployment mode for the resource group.")
                         .When(a => a.ActionType == SpecialVariables.Action.AzureResourceGroup.ResourceGroupActionTypeName);
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.AzureResourceGroup.TemplateSource, "Please provide the template source.")
                         .When(a => a.ActionType == SpecialVariables.Action.AzureResourceGroup.ResourceGroupActionTypeName);
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.Azure.AccountId, "Please provide an account to use for the deployment.")
                         .When(a => a.ActionType == SpecialVariables.Action.AzureResourceGroup.ResourceGroupActionTypeName);
                 });
        }
    }
}