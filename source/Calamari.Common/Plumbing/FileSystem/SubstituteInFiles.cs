using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class SubstituteInFiles : ISubstituteInFiles
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
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
            var filesToTarget = variables.GetPaths(PackageVariables.SubstituteInFilesTargets);
            Substitute(deployment, filesToTarget);
        }

        public void Substitute(RunningDeployment deployment, IList<string> filesToTarget, bool warnIfFileNotFound = true)
        {
            foreach (var target in filesToTarget)
            {
                var matchingFiles = MatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    if (warnIfFileNotFound && deployment.Variables.GetFlag(PackageVariables.EnableNoMatchWarning, true))
                        log.WarnFormat("No files were found that match the substitution target pattern '{0}'", target);

                    continue;
                }

                foreach (var file in matchingFiles)
                    substituter.PerformSubstitution(file, deployment.Variables);
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