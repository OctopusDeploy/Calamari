using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public class NonSensitiveFileSubstituter : FileSubstituter, INonSensitiveFileSubstituter
    {
        public NonSensitiveFileSubstituter(ILog log, ICalamariFileSystem fileSystem, INonSensitiveVariables nonSensitiveVariables)
            : base(log, fileSystem, nonSensitiveVariables)
        {
        }

        protected override void PerformSubstitution(string sourceFile, string targetFile, bool throwOnError)
        {
            try
            {
                //we always want to throw an exception when substitution fails
                base.PerformSubstitution(sourceFile, targetFile, true);
            }
            catch (Exception e)
            {
                throw new CommandException($"{e.Message}{Environment.NewLine}This may be due to sensitive variables in use");
            }
        }
    }
}