using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
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