using System.Text.RegularExpressions;

namespace Calamari.Util
{
    public static class GoDurationParser
    {
        /// <summary>
        /// https://golang.org/pkg/time/#ParseDuration
        /// A duration string is a possibly signed sequence of decimal numbers, each with optional fraction
        /// and a unit suffix, such as "300ms", "-1.5h" or "2h45m". Valid time units are "ns", "us" (or "µs"),
        /// "ms", "s", "m", "h".
        /// </summary>
        static readonly Regex DurationRegex = new Regex(@"^[+-]?(\d+(\.\d+)?(ns|us|µs|ms|s|m|h)?)+$");

        public static bool ValidateTimeout(string timeout)
        {
            return DurationRegex.IsMatch(timeout.Trim());
        }
    }
}