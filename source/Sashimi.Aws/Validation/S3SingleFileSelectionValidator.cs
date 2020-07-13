using FluentValidation;

namespace Sashimi.Aws.Validation
{
    class S3SingleFileSelectionValidator : AbstractValidator<S3FileSelectionProperties>
    {
        public S3SingleFileSelectionValidator()
        {
            RuleFor(x => x.Path).NotEmpty();
            RuleFor(x => x.BucketKey).NotEmpty().When(x => x.BucketKeyBehaviour== BucketKeyBehaviourType.Custom);
        }
    }
}