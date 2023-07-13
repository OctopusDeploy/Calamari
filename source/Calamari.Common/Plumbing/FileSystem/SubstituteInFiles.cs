using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
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

        public void SubstituteBasedSettingsInSuppliedVariables(string currentDirectory)
        {
            var filesToTarget = variables.GetPaths(PackageVariables.SubstituteInFilesTargets)
                                         .SelectMany(v => v.Split(';')).ToArray();
            Substitute(currentDirectory, filesToTarget);
        }

        public void Substitute(string currentDirectory, IList<string> filesToTarget, bool warnIfFileNotFound = true)
        {
            foreach (var target in filesToTarget)
            {
                var matchingFiles = MatchingFiles(currentDirectory, target);

                if (!matchingFiles.Any())
                {
                    if (warnIfFileNotFound && variables.GetFlag(PackageVariables.EnableNoMatchWarning, true))
                        log.WarnFormat("No files were found in {0} that match the substitution target pattern '{1}'", currentDirectory, target);

                    continue;
                }

                foreach (var file in matchingFiles)
                    substituter.PerformSubstitution(file, variables);
            }
        }

        List<string> MatchingFiles(string currentDirectory, string target)
        {
            var globMode = GlobModeRetriever.GetFromVariables(variables);
            var files = fileSystem.EnumerateFilesWithGlob(currentDirectory, globMode, target).Select(Path.GetFullPath).ToList();

            foreach (var path in variables.GetStrings(ActionVariables.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, globMode, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}