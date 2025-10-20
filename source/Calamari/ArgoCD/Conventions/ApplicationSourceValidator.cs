using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;

namespace Calamari.ArgoCD.Conventions
{
    public static class ApplicationSourceValidator
    {
        public static void ValidateApplicationSources(Application application)
        {
            var groupedByName = application.Spec.Sources.GroupBy(s => s.Name.ToApplicationSourceName());

            foreach (var group in groupedByName)
            {
                if (group.Key != null && group.Count() > 1)
                {
                    throw new CommandException($"Application {application.Metadata.Name} has multiples sources with the name '{group.Key}'. Please ensure all sources have unique names.");                   
                }
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> GetUnnamedAnnotations(IDictionary<string, string> annotations)
        {
            var unprocessedKeys = new []
            {
                ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null),
                ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null),
                ArgoCDConstants.Annotations.OctopusTenantAnnotationKey(null)
            };
            
            return annotations.Where(a => unprocessedKeys.Contains(a.Key));
        }

        public static bool ContainsMultipleSourcesAndUnnamedAnnotations(Application application)
        {
            if (application.Spec.Sources.Count <= 1)
            {
                return false;
            }

            if (application.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)))
            {
                return true;
            }
            
            if (application.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)))
            {
                return true;
            }

            if (application.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusTenantAnnotationKey(null)))
            {
                return true;
            }

            return false;
        }
    }
}