using FluentValidation;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountValidator : AbstractValidator<GoogleCloudAccountDetails>
    {
        public GoogleCloudAccountValidator()
        {
            RuleFor(p => p.AccountEmail).NotEmpty().WithMessage("Service account email is required.");
            RuleFor(p => p.JsonKey).NotEmpty().WithMessage("JSON credential is required.");
        }
    }
}