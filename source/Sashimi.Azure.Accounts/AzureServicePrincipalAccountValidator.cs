using System;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountValidator : AbstractValidator<AzureServicePrincipalAccountDetails>
    {
        public AzureServicePrincipalAccountValidator()
        {
            RuleFor(account => account.SubscriptionNumber)
                .Matches(new Regex("^[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}$", RegexOptions.IgnoreCase))
                .WithMessage("Subscription ID should be a GUID in the format 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'.");

            RuleFor(account => account.ClientId)
                .Matches(new Regex("^[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}$", RegexOptions.IgnoreCase))
                .WithMessage("Application ID should be a GUID in the format 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'.");

            RuleFor(account => account.TenantId)
                .Matches(new Regex("^[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}$", RegexOptions.IgnoreCase))
                .WithMessage("Tenant ID should be a GUID in the format 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'.");

            RuleFor(account => account.Password)
                .NotEmpty()
                .WithMessage("Must provide a Azure Service Principal key/password.");

            RuleFor(account => account.ActiveDirectoryEndpointBaseUri)
                .NotEmpty()
                .WithMessage("Active Directory Endpoint Base Uri can't be empty if an isolated Azure Environment was selected")
                .When(account => !string.IsNullOrEmpty(account.AzureEnvironment));

            RuleFor(account => account.ResourceManagementEndpointBaseUri)
                .NotEmpty()
                .WithMessage("Resource Management Endpoint Base Uri can't be empty if an isolated Azure Environment was selected")
                .When(account => !string.IsNullOrEmpty(account.AzureEnvironment));
        }
    }
}