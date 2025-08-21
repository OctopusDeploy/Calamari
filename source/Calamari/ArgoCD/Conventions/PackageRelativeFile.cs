#nullable enable
using System;

namespace Calamari.ArgoCD.Conventions
{
    public interface IPackageRelativeFile
    {
        string AbsolutePath { get; }
        string PackageRelativePath { get; }
    }

    public class PackageRelativeFile : IPackageRelativeFile
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