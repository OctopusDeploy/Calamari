using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToStagingDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment)
        {
            var targetPath = Path.Combine(Environment.CurrentDirectory, "staging"); 
            fileSystem.EnsureDirectoryExists(targetPath);
            return targetPath;
        }
    }
}