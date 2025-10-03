using System.Collections.Generic;

namespace Calamari.Common.Features.Substitutions
{
    public interface ISubstituteFileMatcher
    {
        List<string> FindMatchingFiles(string currentDirectory, string target);
    }
}