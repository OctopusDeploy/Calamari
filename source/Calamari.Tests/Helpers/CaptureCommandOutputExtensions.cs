using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Helpers
{
    public static class CaptureCommandOutputExtensions
    {
        public static string ToApprovalString(this CaptureCommandInvocationOutputSink output)
        {
            return output
                .AllMessages
                .RemoveIgnoredLines()
                .TrimRedactedLines()
                .Aggregate(new StringBuilder(), (builder, s) => builder.AppendLine(s), builder => builder.ToString())
                .ScrubGuids()
                .ScrubTimestampTempFolders();
        }

        private static readonly Regex[] TrimLinesMatching =
        {
            new Regex(@"Octopus Deploy: Calamari version", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"PSVersion                      ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"WSManStackVersion              ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"SerializationVersion           ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"CLRVersion                     ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"BuildVersion                   ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"PSCompatibleVersions           ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"PSRemotingProtocolVersion      ", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        private static readonly Regex[] IgnoreLinesMatching =
        {
            //new Regex(@"THING TO REMOVE", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        public static IEnumerable<string> RemoveIgnoredLines(this IEnumerable<string> lines)
        {
            return lines.Where(line => !IgnoreLinesMatching.Any(regex => regex.IsMatch(line)));
        }
        public static IEnumerable<string> TrimRedactedLines(this IEnumerable<string> lines)
        {
            return lines.Select(line => TrimLinesMatching.FirstOrDefault(regex => regex.IsMatch(line))?.ToString() ?? line);
        }

        private static readonly Regex GuidScrubber = new Regex(@"[{(]?[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}[)}]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static string ScrubGuids(this string subject)
        {
            return GuidScrubber.Replace(subject, "<GUID>");
        }

        private static readonly Regex TimestampTempFolderScrubber = new Regex(@"\d{14}\-\d*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static string ScrubTimestampTempFolders(this string subject)
        {
            return TimestampTempFolderScrubber.Replace(subject, "<TIMESTAMP-TEMP-FOLDER>");
        }
    }
}