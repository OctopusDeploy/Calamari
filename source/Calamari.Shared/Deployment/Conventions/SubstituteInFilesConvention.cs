using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInFilesConvention : IInstallConvention
    {
        readonly Func<RunningDeployment, IEnumerable<string>> targetedFileTargets;
        readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public delegate SubstituteInFilesConvention Factory(Func<RunningDeployment, IEnumerable<string>> getTargetedFiles);

        public SubstituteInFilesConvention(
            ICalamariFileSystem fileSystem, 
            IFileSubstituter substituter,
            Func<RunningDeployment, IEnumerable<string>> getTargetedFiles
        )
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            this.targetedFileTargets = getTargetedFiles;
        }

        public void Install(RunningDeployment deployment)
        {
            foreach (var target in targetedFileTargets(deployment))
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

        private List<string> MatchingFiles(RunningDeployment deployment, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, target).Select(Path.GetFullPath).ToList();

            foreach (var path in deployment.Variables.GetStrings(SpecialVariables.Action.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}