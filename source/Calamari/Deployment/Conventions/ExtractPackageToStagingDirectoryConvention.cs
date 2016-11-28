using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Packages;
using Calamari.Util;
using ICalamariFileSystem = Calamari.Extensibility.FileSystem.ICalamariFileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToWorkingDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToWorkingDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem)
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            return CrossPlatform.GetCurrentDirectory();
        }
    }

    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToStagingDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            var targetPath = Path.Combine(CrossPlatform.GetCurrentDirectory(), "staging"); 
            fileSystem.EnsureDirectoryExists(targetPath);
            return targetPath;
        }
    }
}