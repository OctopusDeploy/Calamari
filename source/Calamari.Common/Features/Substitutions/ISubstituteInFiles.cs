using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.Substitutions
{
    public interface ISubstituteInFiles
    {
        void SubstituteBasedSettingsInSuppliedVariables(string currentDirectory);
        void SubstituteBasedSettingsInSuppliedVariables(string currentDirectory, string[] filesToTarget);
        void Substitute(string currentDirectory, IList<string> filesToTarget, bool warnIfFileNotFound = true);
    }
}