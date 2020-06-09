using FluentValidation;

namespace Sashimi.AzureCloudService.Endpoints
{
    class AzureCloudServiceEndpointValidator : AbstractValidator<AzureCloudServiceEndpoint>
    {
        public AzureCloudServiceEndpointValidator()
        {
            RuleFor(p => p.AccountId).NotEmpty().OverridePropertyName("Account");
            RuleFor(p => p.CloudServiceName).NotEmpty();
            RuleFor(p => p.StorageAccountName).NotEmpty();
        }
    }
}