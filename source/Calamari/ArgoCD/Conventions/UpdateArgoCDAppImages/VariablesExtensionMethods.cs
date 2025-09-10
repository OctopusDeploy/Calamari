using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public static class VariablesExtensionMethods
    {
        public static IList<string> GetContainerPackageNames(this IVariables variables)
        {
            var packageIndexes = variables.GetIndexes(SpecialVariables.Packages.PackageCollection);
            var packageReferences = (from packageIndex in packageIndexes
                                     let image = variables.Get(SpecialVariables.Packages.Image(packageIndex), string.Empty)
                                     let purpose = variables.Get(SpecialVariables.Packages.Purpose(packageIndex), string.Empty)
                                     where purpose.Equals("DockerImageReference", StringComparison.Ordinal)
                                     select image)
                .ToList();

            return packageReferences;
        }
    }
}