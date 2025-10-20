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
    }
}