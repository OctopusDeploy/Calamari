using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Util
{
     public class RelativeGlobMatch
    {
        public string MappedRelativePath { get; }
        public string WorkingDirectory { get; }
        public string FilePath { get; }

        public RelativeGlobMatch(string filePath, string mappedRelativePath, string workingDirectory)
        {
            MappedRelativePath = mappedRelativePath;
            WorkingDirectory = workingDirectory;
            FilePath = filePath;
        }
    }

    public class RelativeGlobber
    {
        private readonly Func<string, string, IEnumerable<string>> enumerateWithGlob;
        public string WorkingDirectory { get; }

        public RelativeGlobber(Func<string, string, IEnumerable<string>> enumerateWithGlob, string workingDirectory)
        {
            this.enumerateWithGlob = enumerateWithGlob;
            WorkingDirectory = workingDirectory;
        }

        private (string glob, string? output) ParsePattern(string pattern)
        {
            var segments = Regex.Split(pattern, "=>");
            var output = segments.Length > 1 ? segments[1].Trim() : null;
            var glob = segments.First().Trim();

            return (glob, output);
        }

        public IEnumerable<RelativeGlobMatch> EnumerateFilesWithGlob(string pattern)
        {
            var (glob, outputPattern) = ParsePattern(pattern);
            var strategy = GetBasePathStrategy(outputPattern);
            var result = enumerateWithGlob(WorkingDirectory, glob);

            return result.Select(x => new RelativeGlobMatch(x, strategy(glob, WorkingDirectory, x).Replace("\\","/"), WorkingDirectory));
        }

        private Func<string, string, string, string> GetBasePathStrategy(string? outputPattern)
        {
            if (string.IsNullOrEmpty(outputPattern))
            {
                return (pattern, cwd, file) => GetGlobBase("*", GetBaseSegmentFromGlob(pattern), file.AsRelativePathFrom(cwd));
            }

            if (outputPattern.Contains("**") || !outputPattern.Contains("*"))
            {
                return (pattern, cwd, file) => GetGlobBase(outputPattern, GetBaseSegmentFromGlob(pattern), file.AsRelativePathFrom(cwd));
            }

            //Be careful of Path.GetFileName, it will bite you on linux
            return (pattern, cwd, file) => Path.Combine(outputPattern.Replace("*", string.Empty), new Uri(file).Segments.Last());
        }

        private string GetBaseSegmentFromGlob(string pattern)
        {
            var segments = pattern.Split('/', '\\');
            var result = string.Empty;

            foreach (var segment in segments)
            {
                if (!segment.Contains("*"))
                {
                    result = $"{result}{segment}/";
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private string GetGlobBase(string outputPattern, string segmentBase, string fileSegment)
        {
            return Path.Combine(outputPattern.Replace("*", string.Empty), string.IsNullOrEmpty(segmentBase) ? fileSegment : fileSegment.Replace(segmentBase, string.Empty));
        }
    }
}