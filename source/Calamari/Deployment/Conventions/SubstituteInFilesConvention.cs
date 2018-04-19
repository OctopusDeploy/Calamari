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
        private readonly Func<RunningDeployment, bool> predicate;
        private readonly Func<RunningDeployment, IEnumerable<string>> fileTargets;
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public SubstituteInFilesConvention(ICalamariFileSystem fileSystem, IFileSubstituter substituter)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            predicate = (deployment) => deployment.Variables.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled);
            fileTargets = (deployment) => deployment.Variables.GetPaths(SpecialVariables.Package.SubstituteInFilesTargets);
        }

        public SubstituteInFilesConvention(ICalamariFileSystem fileSystem, IFileSubstituter substituter,
            Func<RunningDeployment, bool> predicate,
            Func<RunningDeployment, IEnumerable<string>> fileTargetFactory):this(fileSystem, substituter)
        {
            this.predicate = predicate;
            this.fileTargets = fileTargetFactory;
        }
        
        public void Install(RunningDeployment deployment)
        {
            if (!predicate(deployment))
                return;

            foreach (var target in fileTargets(deployment))
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
                    Log.Info("Performing variable substitution on '{0}'", file);
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