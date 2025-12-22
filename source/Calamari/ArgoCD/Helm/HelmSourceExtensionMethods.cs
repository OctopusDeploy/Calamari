using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;

namespace Calamari.ArgoCD.Helm
{
    public static class HelmSourceExtensionMethods
    {
        public static string GenerateInlineValuesAbsolutePath(this ApplicationSource helmApplicationSource, string fileName)
        {
            var path = helmApplicationSource.RepoUrl.AddPath(helmApplicationSource.TargetRevision).AddPath(helmApplicationSource.Path).AddPath(fileName);
            return path.AbsoluteUri;
        }

        public static IEnumerable<string> GenerateValuesFilePaths(this ApplicationSource helmApplicationSource)
        {
            return helmApplicationSource.Helm.ValueFiles.Select(file => file.StartsWith("$")
                                                    ? file
                                                    : helmApplicationSource.GenerateInlineValuesAbsolutePath(file));
        }
    }
}