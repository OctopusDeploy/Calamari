using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInFiles : ISubstituteInFiles
    {
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;
        readonly IVariables variables;

        public SubstituteInFiles(ICalamariFileSystem fileSystem, IFileSubstituter substituter, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            this.variables = variables;
        }

        public void SubstituteBasedSettingsInSuppliedVariables(RunningDeployment deployment)
        {
            var substituteInFilesEnabled = variables.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled);
            if (!substituteInFilesEnabled)
                return;
            
            var filesToTarget = variables.GetPaths(SpecialVariables.Package.SubstituteInFilesTargets);
            Substitute(deployment, filesToTarget);
        }

        public void Substitute(RunningDeployment deployment, IList<string> filesToTarget)
        {
            foreach (var target in filesToTarget)
            {
                var matchingFiles = MatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    if (deployment.Variables.GetFlag(SpecialVariables.Package.EnableNoMatchWarning, true))
                    {
                        Log.WarnFormat("No files were found that match the substitution target pattern '{0}'", target);
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

            foreach (var path in variables.GetStrings(SpecialVariables.Action.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}