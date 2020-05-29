using Calamari.Aws;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using Sashimi.Server.Contracts.Validation;
using PropertiesDictionary = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace Sashimi.Aws.Validation
{
    public class AwsUploadS3DeploymentValidator : AwsDeploymentValidatorBase
    {
        public AwsUploadS3DeploymentValidator() : base(AwsActionTypes.UploadS3)
        {
        }

        public override void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            base.AddDeploymentValidationRule(validator);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.S3.BucketName, "Please provide the bucket name")
                .When(ThisAction);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.S3.FileSelections, "Must specify at least one file selection")
                .When(a => IsS3TargetType("FileSelections", a.Properties));

            validator.RuleFor(a => a.Properties)
                .ValidateSerializedProperty(AwsSpecialVariables.Action.Aws.S3.FileSelections, new S3FileSelectionsValidator())
                .When(a => IsS3TargetType("FileSelections", a.Properties));

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.S3.PackageOptions, "Must provide package options")
                .When(a => IsS3TargetType("EntirePackage", a.Properties));

            validator.RuleFor(a => a.Properties)
                .ValidateSerializedProperty(AwsSpecialVariables.Action.Aws.S3.PackageOptions, new S3PackageOptionsValidator())
                .When(a => IsS3TargetType("EntirePackage", a.Properties));

            validator.RuleFor(a => a.Packages)
                .MustHaveExactlyOnePackage("Please provide the S3 file(s) package.")
                .When(ThisAction);
        }

        static bool IsS3TargetType(string value, PropertiesDictionary properties)
        {
            return properties.TryGetValue(AwsSpecialVariables.Action.Aws.S3.TargetMode, out var targetMode)
                && targetMode == value;
        }
    }
}