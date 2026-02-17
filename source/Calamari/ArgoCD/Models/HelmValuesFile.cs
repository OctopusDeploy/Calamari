using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class HelmValuesFile
    {
        public HelmValuesFile(string defaultClusterRegistry,
                              string path,
                              string fileName)
        {
            Path = path;
            DefaultClusterRegistry = defaultClusterRegistry;
            FileName = fileName;
        }
        public string Path { get; }
        public string DefaultClusterRegistry { get; }
        public string FileName { get; }
    }
}