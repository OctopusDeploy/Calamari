using FluentValidation;

namespace Sashimi.AzureCloudService.Endpoints
{
    class AzureCloudServiceEndpointValidator : AbstractValidator<AzureCloudServiceEndpoint>
    {
        public AzureCloudServiceEndpointValidator()
        {
            RuleFor(p => p.AccountId).NotEmpty().OverridePropertyName("Account");
            // RuleFor(p => p.AccountId)
            //     .Must(x => !string.IsNullOrEmpty(x) && accountTypeRetriever.GetAccountType(x) == AccountTypes.AzureSubscriptionAccountType)
            //     .WithMessage("Only Azure management-certificate accounts may be selected")
            //     .OverridePropertyName("Account");
            RuleFor(p => p.CloudServiceName).NotEmpty();
            RuleFor(p => p.StorageAccountName).NotEmpty();
        }
    }
}