
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
{
    public static class HelmSourceExtensionMethods
    {
        public static string GenerateInlineValuesAbsolutePath(this HelmSource helmSource, string fileName)
        {
            var path = helmSource.RepoUrl.AddPath(helmSource.TargetRevision).AddPath(helmSource.Path).AddPath(fileName);
            return path.AbsoluteUri;
        }

        public static IEnumerable<string> GenerateValuesFilePaths(this HelmSource helmSource)
        {
            return helmSource.Helm.ValueFiles.Select(file => file.StartsWith("$")
                                                    ? file
                                                    : helmSource.GenerateInlineValuesAbsolutePath(file));
        }
    }
}