using FluentValidation;

namespace Sashimi.AzureAppService.Endpoints
{
    class AzureWebAppEndpointValidator : AbstractValidator<AzureWebAppEndpoint>
    {
        public AzureWebAppEndpointValidator()
        {
            RuleFor(p => p.AccountId).NotEmpty().WithName("Account");
            RuleFor(p => p.WebAppName).NotEmpty().WithName("Web App");
            RuleFor(p => p.ResourceGroupName).NotEmpty().WithName("Resource Group");
        }
    }
}