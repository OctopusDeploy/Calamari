namespace Calamari.Common.Features.Processes
{
    public class SilentProcessRunnerResult
    {
        public int ExitCode { get; }

        public string ErrorOutput { get; }

        public SilentProcessRunnerResult(int exitCode, string errorOutput)
        {
            ExitCode = exitCode;
            ErrorOutput = errorOutput;
        }
    }
}
