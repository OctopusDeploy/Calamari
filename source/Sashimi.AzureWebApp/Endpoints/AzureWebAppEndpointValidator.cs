using FluentValidation;

namespace Sashimi.AzureWebApp.Endpoints
{
    class AzureWebAppEndpointValidator : AbstractValidator<AzureWebAppEndpoint>
    {
        public AzureWebAppEndpointValidator()
        {
            RuleFor(p => p.AccountId).NotEmpty().OverridePropertyName("Account");
            RuleFor(p => p.WebAppName).NotEmpty().OverridePropertyName("Web App");
            RuleFor(p => p.ResourceGroupName).NotEmpty().OverridePropertyName("Resources group");
        }
    }
}