using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    static class ApplicationValidator
    {
        public static ValidationResult Validate(Application application)
        {
            return ValidationResult.Merge(
                                          ValidateSourceNames(application),
                                          ValidateUnnamedAnnotationsInMultiSourceApplication(application),
                                          ValidateSourceTypes(application)
                                         );
        }

        static ValidationResult ValidateSourceTypes(Application application)
        {
            if (application.Spec.Sources.Count == application.Status.SourceTypes.Count)
                return ValidationResult.Success;
            
            return ValidationResult.Error($"Application '{application.Metadata.Name}' has sources with undetected source types. Please ensure the application is configured correctly in Argo CD.");

        }

        static ValidationResult ValidateSourceNames(Application application)
        {
            var groupedByName = application.Spec.Sources.GroupBy(s => s.Name.ToApplicationSourceName());

            var groupsWithDuplicates = groupedByName.Where(g => g.Key != null && g.Count() > 1).ToArray();

            return groupsWithDuplicates.Any() 
                ? ValidationResult.Error(groupsWithDuplicates.Select(g => $"Application '{application.Metadata.Name}' has multiples sources with the name '{g.Key}'. Please ensure all sources have unique names.").ToArray()) 
                : ValidationResult.Success;
        }

        static ValidationResult ValidateUnnamedAnnotationsInMultiSourceApplication(Application application)
        {
            if (application.Spec.Sources.Count <= 1)
                return ValidationResult.Success;
            
            var unnamedAnnotations = ArgoCDConstants.Annotations.GetUnnamedAnnotationKeys()
                                                    .Intersect(application.Metadata.Annotations.Keys)
                                                    .ToArray();

            if (unnamedAnnotations.Any())
            {
                return ValidationResult.Warning($"The application '{application.Metadata.Name}' requires all annotations to be qualified by source name since it contains multiple sources. Found these unqualified annotations: {string.Join(", ", unnamedAnnotations.Select(a => $"'{a}'"))}.");
            }
            return ValidationResult.Success;
        }
    }

    class ValidationResult
    {
        public static readonly ValidationResult Success = new ValidationResult(new string[] { }, new string[] { });
        public ValidationResult(IReadOnlyCollection<string> errors, IReadOnlyCollection<string> warnings)
        {
            Errors = errors;
            Warnings = warnings;
        }

        public IReadOnlyCollection<string> Errors { get;  }
        public IReadOnlyCollection<string> Warnings { get;  }

        public static ValidationResult Warning(params string[] messages) => new ValidationResult(new string[] { }, messages);
        public static ValidationResult Error(params string[] messages) => new ValidationResult(messages, new string[] { });

        public static ValidationResult Merge(params ValidationResult[] results)
        {
            return new ValidationResult(results.SelectMany(r => r.Errors).ToArray(), 
                                        results.SelectMany(r => r.Warnings).ToArray());
        }
    }
}