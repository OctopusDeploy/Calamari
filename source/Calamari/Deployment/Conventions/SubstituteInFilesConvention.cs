using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInFilesConvention :  Calamari.Shared.Commands.IConvention
    {
        private readonly Func<IExecutionContext, bool> predicate;
        private readonly Func<IExecutionContext, IEnumerable<string>> fileTargets;
        private readonly ICalamariFileSystem fileSystem;
        readonly IFileSubstituter substituter;

        public SubstituteInFilesConvention(ICalamariFileSystem fileSystem, IFileSubstituter substituter,
            Func<IExecutionContext, bool> predicate = null,
            Func<IExecutionContext, IEnumerable<string>> fileTargetFactory = null)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            
            this.predicate = predicate ?? ((deployment) => deployment.Variables.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled));
            this.fileTargets = fileTargetFactory ?? ((deployment) => deployment.Variables.GetPaths(SpecialVariables.Package.SubstituteInFilesTargets));
        }
        
        private List<string> MatchingFiles(IExecutionContext deployment, string target)
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

        public void Run(IExecutionContext deployment)
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
                    substituter.PerformSubstitution(file, deployment.Variables);
                }
            }
        }
    }
}