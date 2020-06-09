using FluentValidation;

namespace Sashimi.AzureWebApp.Endpoints
{
    class AzureWebAppEndpointValidator : AbstractValidator<AzureWebAppEndpoint>
    {
        public AzureWebAppEndpointValidator()
        {
            RuleFor(p => p.AccountId).NotEmpty().OverridePropertyName("Account");
            RuleFor(p => p.WebAppName).NotEmpty().OverridePropertyName("WebApp");
            RuleFor(p => p.ResourceGroupName).NotEmpty().OverridePropertyName("ResourceGroup");
        }
    }
}