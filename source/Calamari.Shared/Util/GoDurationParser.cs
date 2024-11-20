using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Octopus.CoreUtilities.Extensions;

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
        static readonly Regex DurationRegex = new Regex(@"([+-]?\d+(\.\d+)?)(?=ns|us|µs|ms|s|m|h)(ns|us|µs|ms|s|m|h)?", RegexOptions.Compiled);

        public static bool ValidateDuration(string? duration)
            => !string.IsNullOrWhiteSpace(duration) && 
               (double.TryParse(duration, out _) || DurationRegex.IsMatch(duration.Trim()));

        static readonly List<(string Abbreviation, TimeSpan InitialTimeSpan)> TimeSpanInfos = new List<(string Abbreviation, TimeSpan InitialTimeSpan)>
        {
            ("h", TimeSpan.FromHours(1)), // Hour
            ("m", TimeSpan.FromMinutes(1)), // Minute
            ("s", TimeSpan.FromSeconds(1)), // Second
            ("ms", TimeSpan.FromMilliseconds(1)), //Millisecond
        };

        public static TimeSpan ParseDuration(string duration)
        {
            return ParseDuration(duration, false);
        }

        static TimeSpan ParseDuration(string duration, bool hasBeenValidated)
        {
            if (!hasBeenValidated && !ValidateDuration(duration))
                throw new ArgumentException("Provided duration is not a valid GoLang duration string.");

            //if we can parse this plain value, we assume seconds
            if (double.TryParse(duration, out var dbl))
            {
                return TimeSpan.FromSeconds(dbl);
            }

            var matches = DurationRegex.Matches(duration);

            var result = matches
                         .Cast<Match>()
                         .Select(m =>
                                 {
                                     var value = m.Groups[1].Value;
                                     var abbreviation = m.Groups[3].Value;
                                     var result = TimeSpanInfos.SingleOrDefault(tsi => tsi.Abbreviation == abbreviation);
                                     
                                     if (result == default)
                                     {
                                         return TimeSpan.Zero;
                                     }

#if NETFRAMEWORK
                                     return TimeSpan.FromTicks((long)Math.Floor(result.InitialTimeSpan.Ticks * double.Parse(value)));
#else
                                     return result.InitialTimeSpan * double.Parse(value);
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
                timespan = ParseDuration(duration, true);
                return true;
            }

            timespan = default;
            return false;
        }
    }
}