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

            When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureClientCertificate,
                () =>
                {
                    RuleFor(a => a.ClientCertVariable).NotEmpty();
                    RuleFor(a => a.CertificateStoreLocation).NotEmpty();
                    RuleFor(a => a.CertificateStoreName).NotEmpty();
                });
            
            When(a => a.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD,
                () =>
                {
                    RuleFor(a => a.AadCredentialType)
                        .Equal(AzureServiceFabricCredentialType.UserCredential)
                        .WithName("Azure AD Credential Type");

                    RuleFor(a => a.AadUserCredentialUsername)
                        .NotEmpty()
                        .WithName("Azure AD User Credential Username");
                    RuleFor(a => a.AadUserCredentialPassword)
                        .NotEmpty()
                        .WithName("Azure AD User Credential Password");
                });
        }
    }
}