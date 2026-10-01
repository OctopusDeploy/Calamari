using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class HelmValuesFileImageUpdateTarget 
    {
        public HelmValuesFileImageUpdateTarget(string defaultClusterRegistry,
                                               string path,
                                               string fileName,
                                               IReadOnlyCollection<string> imagePathDefinitions)
        {
            Path = path;
            DefaultClusterRegistry = defaultClusterRegistry;
            FileName = fileName;
            ImagePathDefinitions = imagePathDefinitions;
        }

        public string Path { get; }
        public string DefaultClusterRegistry { get; }
        
        public string FileName { get; }
        public IReadOnlyCollection<string> ImagePathDefinitions { get; }
    }
}
