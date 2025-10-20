using System;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Features.Scripting.DotnetScript
{
    public class DotnetScriptCompilationWarningOutputSink : ICommandInvocationOutputSink
    {
        public bool SuccessfullyCompiled { get; private set; }

        public void WriteInfo(string line) => CheckIfExecutingLine(line);

        public void WriteError(string line) => CheckIfExecutingLine(line);

        void CheckIfExecutingLine(string line)
        {
            if (!SuccessfullyCompiled && string.Equals(line, "Script compiled successfully, executing...", StringComparison.OrdinalIgnoreCase))
            {
                SuccessfullyCompiled = true;
            }
        }

        /// <summary>
        /// Marks the sink as assuming successful compilation. This results in the warning message not being outputted
        /// </summary>
        public void AssumeSuccessfullyCompiled()
        {
            SuccessfullyCompiled = true;
        }
    }
}