using System;

namespace Calamari.Common.Features.Processes
{
    public class SilentProcessRunnerResult
    {
        public SilentProcessRunnerResult(int exitCode, string errorOutput, bool timedOut = false)
        {
            ExitCode = exitCode;
            ErrorOutput = errorOutput;
            TimedOut = timedOut;
        }

        public int ExitCode { get; }

        public string ErrorOutput { get; }

        public bool TimedOut { get; }
    }
}