using FluentValidation;
using Sashimi.Server.Contracts.Validation;

namespace Sashimi.Aws.Validation
{
    public class S3PackageOptionsValidator: AbstractValidator<S3PackageProperties>
    {
        public S3PackageOptionsValidator()
        {
            RuleFor(x => x.BucketKey).NotEmpty().When(x => x.BucketKeyBehaviour == BucketKeyBehaviourType.Custom);
            RuleFor(x => x.CannedAcl).NotEmpty();
            RuleFor(x => x.StorageClass).NotEmpty();

            RuleFor(x => x.Metadata).MustBeDistinct(x => x.Key,
                singular: "Package options metadata key \"{0}\" has been duplicated. Metadata keys must be unique.",
                plural: "Package options metadata keys \"{0}\" have been duplicated. Metadata keys must be unique."
            );

            RuleFor(x => x.Tags).MustBeDistinct(x => x.Key,
                singular: "Package options tag key \"{0}\" has been duplicated. Tag keys must be unique.",
                plural: "Package options tag keys \"{0}\" have been duplicated. Tag keys must be unique."
            );
        }
    }
}