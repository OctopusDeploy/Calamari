using FluentValidation;

namespace Sashimi.Aws.Validation
{
    class S3MultiFileSelectionValidator : AbstractValidator<S3FileSelectionProperties>
    {
        public S3MultiFileSelectionValidator()
        {
            RuleFor(x => x.Pattern).NotEmpty();
        }
    }
}