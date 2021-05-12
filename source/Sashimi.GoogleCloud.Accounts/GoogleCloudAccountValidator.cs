using FluentValidation;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountValidator : AbstractValidator<GoogleCloudAccountDetails>
    {
        public GoogleCloudAccountValidator()
        {
            RuleFor(p => p.JsonKey).NotEmpty().WithMessage("Json key is required.");
        }
    }
}