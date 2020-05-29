using FluentValidation;

namespace Sashimi.Aws.Validation
{
    public class S3MultiFileSelectionValidator : AbstractValidator<S3FileSelectionProperties>
    {
        public S3MultiFileSelectionValidator()
        {
            RuleFor(x => x.Pattern).NotEmpty();
        }
    }
}