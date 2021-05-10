using FluentValidation;

namespace Sashimi.GCP.Accounts
{
    class GcpAccountValidator : AbstractValidator<GcpAccountDetails>
    {
        public GcpAccountValidator()
        {
            RuleFor(p => p.ServiceAccountEmail).NotEmpty().WithMessage("Service account email is required.");
            RuleFor(p => p.Json).NotEmpty().WithMessage("JSON credential is required.");
        }
    }
}