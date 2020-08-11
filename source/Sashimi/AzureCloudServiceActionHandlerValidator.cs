using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServiceActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public AzureCloudServiceActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.Azure.CloudServiceActionTypeName,
                 () =>
                 {
                     When(a => a.Properties.ContainsKey(SpecialVariables.Action.Azure.AccountId),
                          () =>
                          {


                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.Azure.CloudServiceName, "Please select a CloudService or provide a variable expression for the CloudService Name to target.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.Azure.StorageAccountName, "Please select a StorageAccount or provide a variable expression for the StorageAccount Name to target.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.Azure.Slot, "Please select a Slot to target, either Staging or Production.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.Azure.SwapIfPossible, "Please select whether to perform a VIP swap if possible.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.Azure.UseCurrentInstanceCount, "Please select whether to use the current instance count or revert to the instance count specified in the CSCFG file.");
                          });
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.Azure.AccountId, "Please select an Account or provide a variable expression for the Account ID to use.")
                         .When(a => a.Properties.ContainsKey(SpecialVariables.Action.Azure.IsLegacyMode));

                 });
        }
    }
}