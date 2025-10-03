using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public class SubstituteInFiles : ISubstituteInFiles
    {
        readonly ILog log;
        readonly ISubstituteFileMatcher fileMatcher;
        readonly IFileSubstituter substituter;
        readonly IVariables variables;

        public SubstituteInFiles(ILog log, ISubstituteFileMatcher fileMatcher, IFileSubstituter substituter, IVariables variables)
        {
            this.log = log;
            this.fileMatcher = fileMatcher;
            this.substituter = substituter;
            this.variables = variables;
        }

        public void SubstituteBasedSettingsInSuppliedVariables(string currentDirectory,
                                                               bool warnIfFileNotFound = true,
                                                               ISubstituteFileMatcher? customFileMatcher = null)
        {
            var filesToTarget = variables.GetPaths(PackageVariables.SubstituteInFilesTargets);
            Substitute(currentDirectory, filesToTarget, warnIfFileNotFound, customFileMatcher);
        }

        public void Substitute(string currentDirectory,
                               IList<string> filesToTarget,
                               bool warnIfFileNotFound = true,
                               ISubstituteFileMatcher? customFileMatcher = null)
        {
            foreach (var target in filesToTarget)
            {
                var usedMatcher = customFileMatcher ?? fileMatcher;
                var matchingFiles = usedMatcher.FindMatchingFiles(currentDirectory, target);

                if (!matchingFiles.Any())
                {
                    if (warnIfFileNotFound && variables.GetFlag(PackageVariables.EnableNoMatchWarning, true))
                        log.WarnFormat("No files were found in {0} that match the substitution target pattern '{1}'", currentDirectory, target);

                    continue;
                }

                foreach (var file in matchingFiles)
                    substituter.PerformSubstitution(file);
            }
        }
    }
}