using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Server.Extensibility.HostServices.Model;
using PropertiesDictionary = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace Sashimi.Server.Contracts.ActionHandlers.Validation
{
    public static class ValidationExtensions
    {
        public static IRuleBuilderOptions<T, PropertiesDictionary> MustHaveProperty<T>(this IRuleBuilder<T, PropertiesDictionary> ruleBuilder,
                                                                                       string property,
                                                                                       string errorMessage)
        {
            return MustHaveProperty(ruleBuilder, property, arg => errorMessage);
        }

        public static IRuleBuilderOptions<T, PropertiesDictionary> MustHaveProperty<T>(this IRuleBuilder<T, PropertiesDictionary> ruleBuilder,
                                                                                       string property,
                                                                                       Func<T, string> messageProvider)
        {
            return ruleBuilder.Must(properties =>
                                        properties.TryGetValue(property, out var value) && !string.IsNullOrEmpty(value)
                                   )
                              .OverridePropertyName(property)
                              .WithMessage(messageProvider);
        }

        public static IRuleBuilderOptions<T, IEnumerable<PackageReference>> MustHaveExactlyOnePackage<T>(this IRuleBuilderInitial<T, IEnumerable<PackageReference>> ruleBuilder, string errorMessage)
        {
            return ruleBuilder
                   .Cascade(CascadeMode.StopOnFirstFailure)
                   .Must(packages =>
                         {
                             var firstPackage = packages?.FirstOrDefault();

                             return firstPackage != null && packages?.Count() == 1 && !string.IsNullOrWhiteSpace(firstPackage.PackageId) && !string.IsNullOrWhiteSpace(firstPackage.FeedIdOrName?.Value);
                         })
                   .WithMessage(errorMessage);
        }
    }
}