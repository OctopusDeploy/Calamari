using FluentValidation;

namespace Sashimi.AzureServiceFabric.Endpoints
{
    class AzureServiceFabricClusterEndpointValidator : AbstractValidator<AzureServiceFabricClusterEndpoint>
    {
        public AzureServiceFabricClusterEndpointValidator()
        {
            RuleFor(a => a.ConnectionEndpoint).NotEmpty();
            RuleFor(a => a.SecurityMode).NotNull();
            RuleFor(a => a.ServerCertThumbprint)
                .NotEmpty()
                .When(a => a.SecurityMode != AzureServiceFabricSecurityMode.Unsecure);
            RuleFor(a => a.ClientCertVariable)
                .NotEmpty()
                .When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureClientCertificate);
            RuleFor(a => a.AadCredentialType)
                .NotEmpty()
                .When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD);
            RuleFor(a => a.AadClientCredentialSecret)
                .NotEmpty()
                .When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD
                           && a.AadCredentialType == AzureServiceFabricCredentialType.ClientCredential);
            RuleFor(a => a.AadUserCredentialUsername)
                .NotEmpty()
                .When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD
                           && a.AadCredentialType == AzureServiceFabricCredentialType.UserCredential);
            RuleFor(a => a.AadUserCredentialPassword)
                .NotEmpty()
                .When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD
                           && a.AadCredentialType == AzureServiceFabricCredentialType.UserCredential);
        }
    }
}