using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Commands;

namespace Calamari.ArgoCD.Conventions
{
    public class ApplicationSourceValidator
    {
        public static void ValidateApplicationSources(Application applicationFromYaml)
        {
            var groupedByName = applicationFromYaml.Spec.Sources.GroupBy(s => s.Name ?? string.Empty);

            foreach (var group in groupedByName)
            {
                if (!string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                {
                    throw new CommandException($"Application {applicationFromYaml.Metadata.Name} has multiples sources with the name '{group.Key}'. Please ensure all sources have unique names.");                   
                }
            }
        }
    }
}