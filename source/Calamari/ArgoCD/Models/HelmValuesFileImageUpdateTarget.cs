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
}
#endif