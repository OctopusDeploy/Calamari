using System;
using FluentValidation;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureServiceFabric
{
    class AzureServiceFabricAppActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public AzureServiceFabricAppActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.ServiceFabric.ServiceFabricAppActionTypeName,
                 () =>
                 {
                     When(a => a.Properties.ContainsKey(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint),
                          () =>
                          {
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.PublishProfileFile, "Please enter a publish profile file.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.ClientCertVariable, "Please enter a client certificate variable.")
                                  .When(a => a.Properties[SpecialVariables.Action.ServiceFabric.SecurityMode] == AzureServiceFabricSecurityMode.SecureClientCertificate.ToString());
                          });

                     When(a => a.Properties.ContainsKey(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint) || a.Properties.ContainsKey(SpecialVariables.Action.ServiceFabric.IsLegacyMode),
                          () =>
                          {
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, "Please enter a connection endpoint.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.SecurityMode, "Please enter a valid security mode.");
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint, "Please enter a server certificate thumbprint.")
                                  .When(a => a.Properties[SpecialVariables.Action.ServiceFabric.SecurityMode] != AzureServiceFabricSecurityMode.Unsecure.ToString());
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.AadCredentialType, "Please select a credential type.")
                                  .When(a => a.Properties[SpecialVariables.Action.ServiceFabric.SecurityMode] == AzureServiceFabricSecurityMode.SecureAzureAD.ToString());
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.AadClientCredentialSecret, "Please enter a client application secret for AAD.")
                                  .When(a => a.Properties[SpecialVariables.Action.ServiceFabric.SecurityMode] == AzureServiceFabricSecurityMode.SecureAzureAD.ToString()
                                             && a.Properties[SpecialVariables.Action.ServiceFabric.AadCredentialType] == AzureServiceFabricCredentialType.ClientCredential.ToString());
                              RuleFor(a => a.Properties)
                                  .MustHaveProperty(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername, "Please enter a username for AAD.")
                                  .When(a => a.Properties[SpecialVariables.Action.ServiceFabric.SecurityMode] == AzureServiceFabricSecurityMode.SecureAzureAD.ToString()
                                             && a.Properties[SpecialVariables.Action.ServiceFabric.AadCredentialType] == AzureServiceFabricCredentialType.UserCredential.ToString());
                          });
                 });
        }
    }
}