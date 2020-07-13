using System.Collections.Generic;
using FluentValidation;
using Sashimi.Server.Contracts.Validation;

namespace Sashimi.Aws.Validation
{
    class S3FileSelectionsValidator : AbstractValidator<List<S3FileSelectionProperties>>
    {
        public S3FileSelectionsValidator()
        {
            RuleFor(x => x).NotNull();
            RuleFor(x => x).NotEmpty().WithMessage("Must specify at least one file selection");
            RuleForEach(x => x).SetValidator(new S3FileSelectionValidator());
        }
    }

    class S3FileSelectionValidator: AbstractValidator<S3FileSelectionProperties>
    {
        public S3FileSelectionValidator()
        {
            RuleFor(x => x.StorageClass).NotEmpty();
            RuleFor(x => x.CannedAcl).NotEmpty();
            RuleFor(x => x).NotNull();
            RuleFor(x => x.Metadata).MustBeDistinct(x => x.Key,
                singular: "File selection metadata key \"{0}\" has been duplicated. Metadata keys must be unique.",
                plural: "File selection metadata keys \"{0}\" have been duplicated. Metadata keys must be unique."
            );

            RuleFor(x => x.Tags).MustBeDistinct(x => x.Key,
                singular: "File selection tag key \"{0}\" has been duplicated. Tag keys must be unique.",
                plural: "File selection tag keys \"{0}\" have been duplicated. Tag keys must be unique."
            );

            RuleFor(x => x).SetValidator(new S3MultiFileSelectionValidator()).When(x => x.Type == "MultipleFiles");
            RuleFor(x => x).SetValidator(new S3SingleFileSelectionValidator()).When(x => x.Type == "SingleFile");

        }
    }
}