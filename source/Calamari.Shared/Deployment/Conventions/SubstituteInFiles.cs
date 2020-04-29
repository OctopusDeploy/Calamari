using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Common.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInFiles : ISubstituteInFiles
    {
        readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;
        readonly IVariables variables;

        public SubstituteInFiles(ILog log, ICalamariFileSystem fileSystem, IFileSubstituter substituter, IVariables variables)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            this.variables = variables;
        }

        public void SubstituteBasedSettingsInSuppliedVariables(RunningDeployment deployment)
        {
            var substituteInFilesEnabled = variables.GetFlag(PackageVariables.SubstituteInFilesEnabled);
            if (!substituteInFilesEnabled)
                return;
            
            var filesToTarget = variables.GetPaths(PackageVariables.SubstituteInFilesTargets);
            Substitute(deployment, filesToTarget);
        }

        public void Substitute(RunningDeployment deployment, IList<string> filesToTarget)
        {
            foreach (var target in filesToTarget)
            {
                var matchingFiles = MatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    if (deployment.Variables.GetFlag(PackageVariables.EnableNoMatchWarning, true))
                    {
                        log.WarnFormat("No files were found that match the substitution target pattern '{0}'", target);
                    }

                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    substituter.PerformSubstitution(file, deployment.Variables);
                }
            }
        }

        List<string> MatchingFiles(RunningDeployment deployment, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, target).Select(Path.GetFullPath).ToList();

            foreach (var path in variables.GetStrings(ActionVariables.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}