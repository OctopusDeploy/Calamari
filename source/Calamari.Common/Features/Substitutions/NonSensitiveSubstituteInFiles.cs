using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public class NonSensitiveSubstituteInFiles : SubstituteInFiles, INonSensitiveSubstituteInFiles
    {
        public NonSensitiveSubstituteInFiles(ILog log, ISubstituteFileMatcher fileMatcher, INonSensitiveFileSubstituter substituter, INonSensitiveVariables variables) 
            : base(log, fileMatcher, substituter, variables)
        {
        }
    }
}