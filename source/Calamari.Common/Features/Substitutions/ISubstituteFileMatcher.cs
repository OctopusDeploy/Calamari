using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public interface ISubstituteFileMatcher
    {
        List<string> FindMatchingFiles(string currentDirectory, string target);
    }

    public class GlobSubstituteFileMatcher : ISubstituteFileMatcher
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;

        public GlobSubstituteFileMatcher(ICalamariFileSystem fileSystem, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
        }
        
        public List<string> FindMatchingFiles(string currentDirectory, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(currentDirectory, target).Select(Path.GetFullPath).ToList();

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