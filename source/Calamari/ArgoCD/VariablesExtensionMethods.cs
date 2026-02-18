using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD
{
    public static class VariablesExtensionMethods
    {
        public static IList<string> GetContainerPackageNames(this IVariables variables)
        {
            return variables.GetIndexes(PackageVariables.PackageCollection)
                                          .Select(pi => variables.Get(PackageVariables.IndexedImage(pi), string.Empty))
                                          .Where(name => !name.IsNullOrEmpty())
                                          .ToList();
        }

        public static DeploymentScope GetDeploymentScope(this IVariables variables)
        {
            return new DeploymentScope(variables.GetMandatoryVariable(ProjectVariables.Slug).ToProjectSlug()!,
                                       variables.GetMandatoryVariable(DeploymentEnvironment.Slug).ToEnvironmentSlug()!,
                                       variables.Get(DeploymentVariables.Tenant.Slug)?.ToTenantSlug());
        }
    }
}