using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Conventions
{
    public class GlobbedSubtituteInFilesConvention : IInstallConvention
    {
        readonly string globVariableName;
        readonly ISubstituteInFiles substituteInFiles;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public GlobbedSubtituteInFilesConvention(string globVariableName, ISubstituteInFiles substituteInFiles, ICalamariFileSystem fileSystem, ILog log)
        {
            this.globVariableName = globVariableName;
            this.substituteInFiles = substituteInFiles;
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            var pathGlobs = deployment.Variables.GetPaths(globVariableName).ToArray();
            var globString = string.Join( ",", pathGlobs);
            log.Verbose($"Using file globs {globString}");
            var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, pathGlobs).ToList();
            log.Verbose($"Substituting variables into {files.Count} files");
            
            foreach (var file in files)
            {
                log.Verbose($"Globbing sub-folder {file}");    
            }
            substituteInFiles.Substitute(deployment.CurrentDirectory, files);
        }
    }
}