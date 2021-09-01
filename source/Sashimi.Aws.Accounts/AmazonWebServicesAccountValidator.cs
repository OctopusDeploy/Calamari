using System;
using FluentValidation;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountValidator : AbstractValidator<AmazonWebServicesAccountDetails>
    {
        public AmazonWebServicesAccountValidator()
        {
            RuleFor(p => p.AccessKey).NotEmpty().WithMessage("Access Key is required.");
            RuleFor(p => p.SecretKey).NotEmpty().WithMessage("Secret Key is required.");
        }
    }
}