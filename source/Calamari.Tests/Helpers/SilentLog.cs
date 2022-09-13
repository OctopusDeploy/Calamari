using Calamari.Common.Plumbing.Logging;

namespace Calamari.Tests.Helpers
{
    public class SilentLog : AbstractLog
    {
        protected override void StdOut(string message)
        {
            // Do nothing
        }

        protected override void StdErr(string message)
        {
            // Do nothing
        }
    }
}