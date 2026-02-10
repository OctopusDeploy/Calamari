using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.ArgoCD
{
    public static class VariablesExtensionMethods
    {
        public static IList<string> GetContainerPackageNames(this IVariables variables)
        {
            var packageIndexes = variables.GetIndexes(PackageVariables.PackageCollection);
            var packageReferences = (from packageIndex in packageIndexes
                                     let image = variables.Get(PackageVariables.IndexedImage(packageIndex), string.Empty)
                                     let purpose = variables.Get(PackageVariables.IndexedPackagePurpose(packageIndex), string.Empty)
                                     where purpose.Equals("DockerImageReference", StringComparison.Ordinal)
                                     select image)
                .ToList();

            return packageReferences;
        }

        public static DeploymentScope GetDeploymentScope(this IVariables variables)
        {
            return new DeploymentScope(variables.GetMandatoryVariable(ProjectVariables.Slug).ToProjectSlug()!,
                                       variables.GetMandatoryVariable(DeploymentEnvironment.Slug).ToEnvironmentSlug()!,
                                       variables.Get(DeploymentVariables.Tenant.Slug)?.ToTenantSlug());
        }
    }
}