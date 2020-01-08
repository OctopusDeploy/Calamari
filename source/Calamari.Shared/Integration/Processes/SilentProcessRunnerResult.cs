namespace Calamari.Integration.Processes
{
    public class SilentProcessRunnerResult
    {
        public int ExitCode { get; }

        public string ErrorOutput { get; }
        public bool TimedOut { get; set; }

        public SilentProcessRunnerResult(int exitCode, string errorOutput, bool timedOut)
        {
            ExitCode = exitCode;
            ErrorOutput = errorOutput;
            TimedOut = timedOut;
        }
    }
}
