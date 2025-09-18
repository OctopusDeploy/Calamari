#if NET
using System;

namespace Calamari.ArgoCD.Models
{
    public class ArgoCDImageUpdateTarget
    {
        public ArgoCDImageUpdateTarget(string name,
                                       string defaultClusterRegistry,
                                       string path,
                                       Uri repoUrl,
                                       string targetRevision)
        {
            Name = name;
            DefaultClusterRegistry = defaultClusterRegistry;
            Path = path;
            RepoUrl = repoUrl;
            TargetRevision = targetRevision;
        }

        public string Name { get; }
        public string DefaultClusterRegistry { get; }
        public string Path { get; }
        public Uri RepoUrl { get; }
        public string TargetRevision { get; }
    }
}
#endif