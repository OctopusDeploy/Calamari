#if NET
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class HelmChartFileImageUpdateTarget : ArgoCDImageUpdateTarget
    {
        public HelmChartFileImageUpdateTarget(string appName,
                                               string defaultClusterRegistry,
                                               string path,
                                               Uri repoUrl,
                                               string targetRevision,
                                               string fileName,
                                               List<string> imagePathDefinitions) : base(appName,
                                                                                         defaultClusterRegistry,
                                                                                         path,
                                                                                         repoUrl,
                                                                                         targetRevision)
        {
            FileName = fileName;
            ImagePathDefinitions = imagePathDefinitions;
        }

        public string FileName { get; }
        public List<string> ImagePathDefinitions { get; }
    }

    // Allows us to pass issues up the chain for logging without pushing an ITaskLog all the way down the stack
    public class InvalidHelmChartFileImageUpdateTarget :

    HelmChartFileImageUpdateTarget
    {
        public InvalidHelmChartFileImageUpdateTarget(string appName,
                                                      string defaultClusterRegistry,
                                                      string path,
                                                      Uri repoUrl,
                                                      string targetRevision,
                                                      string fileName,
                                                      string alias) : base(appName,
                                                                           defaultClusterRegistry,
                                                                           path,
                                                                           repoUrl,
                                                                           targetRevision,
                                                                           fileName,
                                                                           new List<string>())
        {
            Alias = alias;
        }

        public string Alias { get; }
    }
}
#endif