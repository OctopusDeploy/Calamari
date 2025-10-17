#if NET
using System;

namespace Calamari.ArgoCD.Models
{
    public class ArgoCDImageUpdateTarget
    {
        public ArgoCDImageUpdateTarget(ApplicationName name,
                                       ApplicationSourceName sourceName,
                                       string defaultClusterRegistry,
                                       string path,
                                       Uri repoUrl,
                                       string targetRevision)
        {
            Name = name;
            SourceName = sourceName;
            DefaultClusterRegistry = defaultClusterRegistry;
            Path = path;
            RepoUrl = repoUrl;
            TargetRevision = targetRevision;
        }

        public ApplicationName Name { get; }
        
        public ApplicationSourceName SourceName { get; }
        public string DefaultClusterRegistry { get; }
        public string Path { get; }
        public Uri RepoUrl { get; }
        public string TargetRevision { get; }
    }
}
#endif