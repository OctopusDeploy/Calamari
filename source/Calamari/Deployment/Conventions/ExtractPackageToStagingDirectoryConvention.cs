using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Octopus.Versioning.Metadata;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public string SubDirectory { get; private set; }
        
        public ExtractPackageToStagingDirectoryConvention(
            IPackageExtractor extractor, 
            ICalamariFileSystem fileSystem,
            String subDirectory = "staging") 
            : base(extractor, fileSystem)
        {
            SubDirectory = subDirectory;
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            var targetPath = String.IsNullOrEmpty(SubDirectory) ?
                Environment.CurrentDirectory :
                Path.Combine(Environment.CurrentDirectory, SubDirectory); 
            fileSystem.EnsureDirectoryExists(targetPath);
            return targetPath;
        }
    }
}