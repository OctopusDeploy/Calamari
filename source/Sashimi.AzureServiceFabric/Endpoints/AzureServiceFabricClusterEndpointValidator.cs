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
            When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD,
                () =>
                {
                    RuleFor(a => a.AadCredentialType)
                        .NotEqual(AzureServiceFabricCredentialType.ClientCredential)
                        .WithName("Azure AD Credential Type");
                    RuleFor(a => a.AadClientCredentialSecret)
                        .NotEmpty()
                        .When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD
                                   && a.AadCredentialType == AzureServiceFabricCredentialType.ClientCredential);

                    When(a => a.AadCredentialType == AzureServiceFabricCredentialType.UserCredential, () =>
                    {
                        RuleFor(a => a.AadUserCredentialUsername)
                            .NotEmpty()
                            .WithName("Azure AD User Credential Username");
                        RuleFor(a => a.AadUserCredentialPassword)
                            .NotEmpty()
                            .WithName("Azure AD User Credential Password");
                    });
                });
        }
    }
}