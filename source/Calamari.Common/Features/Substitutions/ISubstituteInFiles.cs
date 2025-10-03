using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.Substitutions
{
    public interface ISubstituteInFiles
    {
        void SubstituteBasedSettingsInSuppliedVariables(string currentDirectory,
                                                        bool warnIfFileNotFound = true,
                                                        ISubstituteFileMatcher? customFileMatcher = null);

        void Substitute(string currentDirectory,
                        IList<string> filesToTarget,
                        bool warnIfFileNotFound = true,
                        ISubstituteFileMatcher? customFileMatcher = null);
    }
}