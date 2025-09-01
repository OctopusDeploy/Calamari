#nullable enable
using System;
using System.IO;

namespace Calamari.ArgoCD.Conventions
{
    interface IFileCopySpecification
    {
        string SourceAbsolutePath { get; }
        string DestinationRelativePath { get; }
        string DestinationAbsolutePath { get; }
    }

    public class FileCopySpecification : IFileCopySpecification
    {
        readonly IPackageRelativeFile packageSource;
        readonly string rootPath;
        readonly string repositorySubPath;

        public string SourceAbsolutePath => packageSource.AbsolutePath;
        public string DestinationRelativePath => Path.Combine(repositorySubPath, packageSource.PackageRelativePath);
        public string DestinationAbsolutePath => Path.Combine(rootPath, DestinationRelativePath);

        public FileCopySpecification(IPackageRelativeFile packageSource, string rootPath, string repositorySubPath)
        {
            this.packageSource = packageSource;
            this.rootPath = rootPath;
            this.repositorySubPath = repositorySubPath;
        }
    }
}