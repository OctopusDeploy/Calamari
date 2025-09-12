using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public static class VariablesExtensionMethods
    {
        public static IList<string> GetContainerPackageNames(this IVariables variables)
        {
            var packageIndexes = variables.GetIndexes(PackageVariables.PackageCollection);
            var packageReferences = (from packageIndex in packageIndexes
                                     let image = variables.Get(PackageVariables.IndexedPackageId(packageIndex), string.Empty)
                                     let purpose = variables.Get(PackageVariables.IndexedPackagePurpose(packageIndex), string.Empty)
                                     where purpose.Equals("DockerImageReference", StringComparison.Ordinal)
                                     select image)
                .ToList();

            return packageReferences;
        }
    }
}