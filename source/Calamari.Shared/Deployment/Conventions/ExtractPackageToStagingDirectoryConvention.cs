using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public string SubDirectory { get; private set; }
        
        public ExtractPackageToStagingDirectoryConvention(
            IPackageExtractor extractor, 
            ICalamariFileSystem fileSystem,
            bool extractToSubdirectory = true) 
            : base(extractor, fileSystem)
        {
            SubDirectory = extractToSubdirectory ? "staging" : null;
        }

        protected override string GetTargetPath(RunningDeployment deployment)
        {
            var targetPath = String.IsNullOrEmpty(SubDirectory) ?
                Environment.CurrentDirectory :
                Path.Combine(Environment.CurrentDirectory, SubDirectory); 
            fileSystem.EnsureDirectoryExists(targetPath);
            return targetPath;
        }
    }
}