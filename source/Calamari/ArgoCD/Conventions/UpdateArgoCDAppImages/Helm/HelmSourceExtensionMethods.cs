
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Domain;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
{
    public static class HelmSourceExtensionMethods
    {
        public static string GenerateInlineValuesAbsolutePath(this HelmSource helmSource, string fileName)
        {
            // var path = helmSource.RepositoryUrl.AddPath(helmSource.TargetRevision).AddPath(helmSource.Path).AddPath(fileName);
            var uriAsString = helmSource.RepoUrl.ToString(); 
            var repoURL = uriAsString.EndsWith("/") ? uriAsString : $"{uriAsString}/";

            var path = helmSource.Path.TrimStart('.').TrimStart('/');
            
            var builder = new UriBuilder(repoURL);
            builder.Path += Path.Combine(helmSource.TargetRevision.Trim('/'), path, fileName.Trim('/'));
            builder.Port = -1; //TODO(tmm): Had to manually unser this :/

            return builder.ToString();
        }

        public static IEnumerable<string> GenerateValuesFilePaths(this HelmSource helmSource)
        {
            return helmSource.Helm.ValueFiles.Select(file => file.StartsWith("$")
                                                    ? file
                                                    : helmSource.GenerateInlineValuesAbsolutePath(file));
        }
    }
}