using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public class NonSensitiveFileSubstituter : FileSubstituter, INonSensitiveFileSubstituter
    {
        public NonSensitiveFileSubstituter(ILog log, ICalamariFileSystem fileSystem, INonSensitiveVariables variables)
            : base(log, fileSystem, variables)
        {
        }

        protected override void PerformSubstitutionAndUpdateFile(string sourceFile, string targetFile, bool throwOnError, bool throwPlainOctostacheError = false )
        {
            try
            {
                //We always want to throw when substitution fails
                base.PerformSubstitutionAndUpdateFile(sourceFile, targetFile, true, true);
            }
            catch (InvalidOperationException e)
            {
                throw new CommandException($"{e.Message}. This may be due to invalid Octostache syntax or sensitive variables.");
            }
        }
    }
}