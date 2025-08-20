#nullable enable
using System;

namespace Calamari.ArgoCD.Conventions
{
    public class PackageRelativeFile
    {
        public PackageRelativeFile(string absolutePath, string packageRelativePath)
        {
            PackageRelativePath = packageRelativePath;
            AbsolutePath = absolutePath;
        }
        public string AbsolutePath { get; }
        public string PackageRelativePath { get;  }
    }
}