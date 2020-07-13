using System;

namespace Calamari.Common.Features.Processes
{
    public class SilentProcessRunnerResult
    {
        public SilentProcessRunnerResult(int exitCode, string errorOutput)
        {
            ExitCode = exitCode;
            ErrorOutput = errorOutput;
        }

        public int ExitCode { get; }

        public string ErrorOutput { get; }
    }
}