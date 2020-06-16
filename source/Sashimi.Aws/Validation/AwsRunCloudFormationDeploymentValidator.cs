using System;
using FluentValidation;
using Octopus.CoreUtilities;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using PropertiesDictionary = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace Sashimi.Aws.Validation
{
    class AwsRunCloudFormationDeploymentValidator : AwsDeploymentValidatorBase
    {
        public AwsRunCloudFormationDeploymentValidator() : base(AwsActionTypes.RunCloudFormation)
        {
        }

        public override void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            base.AddDeploymentValidationRule(validator);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.Template, "Please provide the CloudFormation template.")
                .When(ThisAction)
                .When(a => !IsTemplateFromPackage(a.Properties));

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.Template, "Please provide the CloudFormation template path.")
                .When(ThisAction)
                .When(a => IsTemplateFromPackage(a.Properties));

            validator.RuleFor(a => a.Packages)
                .MustHaveExactlyOnePackage("Please provide the CloudFormation template package.")
                .When(ThisAction)
                .When(a => IsTemplateFromPackage(a.Properties));

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.StackName, "Please provide the CloudFormation stack name.")
                .When(ThisAction);


            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Name, "Please provide a name for the change set")
                .When(ThisAction)
                .When(ChangesetsFeatureEnabled)
                .When(FlagDisabled(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Generate));
        }

        static bool IsTemplateFromPackage(PropertiesDictionary properties)
        {
            return properties.TryGetValue(AwsSpecialVariables.Action.Aws.TemplateSource, out var templateSource) &&
                templateSource == "Package";
        }

        static Func<DeploymentActionValidationContext, bool> FlagEnabled(string property)
        {
            return action =>
                GetFlagMaybe(action.Properties, property)
                    .SelectValueOrDefault(x => x);
        }

        static Func<DeploymentActionValidationContext, bool> FlagDisabled(string property)
        {
            return action => !FlagEnabled(property)(action);
        }

        static bool ChangesetsFeatureEnabled(DeploymentActionValidationContext action)
        {
            return GetPropertyMaybe(action.Properties, KnownVariables.Action.EnabledFeatures)
                .SelectValueOrDefault(x => x.Contains(AwsSpecialVariables.Action.Aws.CloudFormation.Changesets.Feature));
        }

        static Maybe<string> GetPropertyMaybe(PropertiesDictionary properties, string property)
        {
            if (properties.TryGetValue(property, out var value) && !string.IsNullOrEmpty(value))
            {
                return Maybe<string>.Some(value);
            }

            return Maybe<string>.None;
        }

        static Maybe<bool> GetFlagMaybe(PropertiesDictionary properties, string property)
        {
            return GetPropertyMaybe(properties, property)
                .SelectValueOr(ParseBoolMaybe, Maybe<bool>.None);
        }

        static Maybe<bool> ParseBoolMaybe(string value)
        {
            return bool.TryParse(value, out var result) ? Maybe<bool>.Some(result) : Maybe<bool>.None;
        }
    }
}