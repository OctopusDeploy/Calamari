using System;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Sashimi.AzureCloudService
{
    class AzureSubscriptionValidator : AbstractValidator<AzureSubscriptionDetails>
    {
        public AzureSubscriptionValidator()
        {
            RuleFor(account => account.SubscriptionNumber)
                .Matches(new Regex("^[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}$", RegexOptions.IgnoreCase))
                .WithMessage("Subscription ID should be a GUID in the format 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'.");

            RuleFor(account => account.ServiceManagementEndpointSuffix)
                .NotEmpty()
                .WithMessage("Service Management Endpoint Suffix can't be empty if an isolated Azure Environment was selected")
                .When(account => !String.IsNullOrEmpty(account.AzureEnvironment) && account.AccountType == AccountTypes.AzureSubscriptionAccountType);

            RuleFor(account => account.ServiceManagementEndpointBaseUri)
                .NotEmpty()
                .WithMessage("Service Management Endpoint Base URI can't be empty if an isolated Azure Environment was selected")
                .When(account => !String.IsNullOrEmpty(account.AzureEnvironment) && account.AccountType == AccountTypes.AzureSubscriptionAccountType);
        }
    }
}