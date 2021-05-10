using FluentValidation;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountValidator : AbstractValidator<GoogleCloudAccountDetails>
    {
        public GoogleCloudAccountValidator()
        {
            RuleFor(p => p.ServiceAccountEmail).NotEmpty().WithMessage("Service account email is required.");
            RuleFor(p => p.Json).NotEmpty().WithMessage("JSON credential is required.");
        }
    }
}