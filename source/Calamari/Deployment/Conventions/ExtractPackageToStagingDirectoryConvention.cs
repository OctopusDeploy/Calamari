using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public string SubDirectory { get; private set; }
        
        public ExtractPackageToStagingDirectoryConvention(
            IPackageExtractor extractor, 
            ICalamariFileSystem fileSystem,
            ILog log,
            String subDirectory = "staging") 
            : base(extractor, fileSystem, log)
        {
            SubDirectory = subDirectory;
        }

        protected override string GetTargetPath(IExecutionContext deployment)
        {
            var targetPath = String.IsNullOrEmpty(SubDirectory) ?
                Environment.CurrentDirectory :
                Path.Combine(Environment.CurrentDirectory, SubDirectory); 
            fileSystem.EnsureDirectoryExists(targetPath);
            return targetPath;
        }
    }
}