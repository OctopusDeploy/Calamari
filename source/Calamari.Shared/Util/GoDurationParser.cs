using System;
using System.Collections.Generic;
using System.Linq;
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

        public static bool ValidateDuration(string? duration)
            => !string.IsNullOrWhiteSpace(duration) && DurationRegex.IsMatch(duration);

        static List<(string Abbreviation, TimeSpan InitialTimeSpan)> timeSpanInfos = new List<(string Abbreviation, TimeSpan InitialTimeSpan)>
        {
            ("h", TimeSpan.FromHours(1)), // Hour
            ("m", TimeSpan.FromMinutes(1)), // Minute
            ("s", TimeSpan.FromSeconds(1)), // Second
            ("ms", TimeSpan.FromMilliseconds(1)), //Millisecond
            ("t", TimeSpan.FromTicks(1)) // Tick
        };

        public static TimeSpan ParseDuration(string duration)
        {
            var result = timeSpanInfos
                         .Where(timeSpanInfo => duration.Contains(timeSpanInfo.Abbreviation))
                         .Select(timeSpanInfo =>
                                 {
#if NETFRAMEWORK
                                     return TimeSpan.FromTicks(timeSpanInfo.InitialTimeSpan.Ticks * int.Parse(new Regex(@$"(\d+){timeSpanInfo.Abbreviation}").Match(duration).Groups[1].Value));
#else
                                     return timeSpanInfo.InitialTimeSpan * int.Parse(new Regex(@$"(\d+){timeSpanInfo.Abbreviation}").Match(duration).Groups[1].Value);
#endif
                                 })
                         .Aggregate((accumulator, timeSpan) => accumulator + timeSpan);
            return result;
        }

        public static bool TryParseDuration(string? duration, out TimeSpan timespan)
        {
            duration = duration?.Trim();

            if (ValidateDuration(duration))
            {
                timespan = ParseDuration(duration);
                return true;
            }

            timespan = default;
            return false;
        }
    }
}