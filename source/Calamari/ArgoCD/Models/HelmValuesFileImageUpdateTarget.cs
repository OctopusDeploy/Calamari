#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class HelmValuesFileImageUpdateTarget : ArgoCDImageUpdateTarget
    {
        public HelmValuesFileImageUpdateTarget(ApplicationName appName,
                                               ApplicationSourceName sourceName,
                                               string defaultClusterRegistry,
                                               string path,
                                               Uri repoUrl,
                                               string targetRevision,
                                               string fileName,
                                               IReadOnlyCollection<string> imagePathDefinitions) : base(appName,
                                                                                                        sourceName,
                                                                                                        defaultClusterRegistry,
                                                                                                        path,
                                                                                                        repoUrl,
                                                                                                        targetRevision)
        {
            FileName = fileName;
            ImagePathDefinitions = imagePathDefinitions;
        }

        public string FileName { get; }
        public IReadOnlyCollection<string> ImagePathDefinitions { get; }
    }

    public abstract class HelmSourceConfigurationProblem
    {
    }

    public class HelmSourceIsMissingImagePathAnnotation : HelmSourceConfigurationProblem
    {
        public HelmSourceIsMissingImagePathAnnotation(ApplicationSourceName name, Uri repoUrl)
        {
            Name = name;
            RepoUrl = repoUrl;
        }

        public ApplicationSourceName Name { get;  }
        public Uri RepoUrl { get;  }
    }
    
    public class RefSourceIsMissing : HelmSourceConfigurationProblem
    {
        public RefSourceIsMissing(string @ref, ApplicationSourceName helmSourceName, Uri helmSourceRepoUrl)
        {
            Ref = @ref;
            HelmSourceName = helmSourceName;
            HelmSourceRepoUrl = helmSourceRepoUrl;
        }

        public string Ref { get; }
        
        public ApplicationSourceName HelmSourceName { get;  }
        public Uri HelmSourceRepoUrl { get;  }
    }
}
#endif