// Based on Glob.cs from https://github.com/mganss/Glob.cs/
// NuGet is licensed under the Apache license: https://github.com/NuGet/NuGet.Client/blob/dev/LICENSE.txt

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Calamari.Common.Plumbing.FileSystem
{
    /// <summary>
    /// Finds files and directories by matching their path names against a pattern.
    /// </summary>
    public class Glob
    {
        static readonly ConcurrentDictionary<string, RegexOrString> RegexOrStringCache = new ConcurrentDictionary<string, RegexOrString>();

        static readonly char[] GlobCharacters = "*?{}".ToCharArray();

        static readonly HashSet<char> RegexSpecialChars = new HashSet<char>(new[] { '[', '\\', '^', '$', '.', '|', '?', '*', '+', '(', ')' });

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public Glob()
        {
            IgnoreCase = true;
            CacheRegexes = true;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="pattern">The pattern to be matched. See <see cref="Pattern" /> for syntax.</param>
        public Glob(string pattern)
            : this()
        {
            Pattern = pattern;
        }

        /// <summary>
        /// Gets or sets a value indicating the pattern to match file and directory names against.
        /// The pattern can contain the following special characters:
        /// <list type="table">
        ///     <item>
        ///         <term>?</term>
        ///         <description>Matches any single character in a file or directory name.</description>
        ///     </item>
        ///     <item>
        ///         <term>*</term>
        ///         <description>Matches zero or more characters in a file or directory name.</description>
        ///     </item>
        ///     <item>
        ///         <term>**</term>
        ///         <description>Matches zero or more recursive directories.</description>
        ///     </item>
        ///     <item>
        ///         <term>{group1,group2,...}</term>
        ///         <description>Matches any of the pattern groups. Groups can contain groups and patterns.</description>
        ///     </item>
        ///     <item>
        ///         <term>[...]</term>
        ///         <description>Matches a set of characters in a name. Syntax is equivalent to character groups in <see cref="System.Text.RegularExpressions.Regex" />.</description>
        ///     </item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// Note: [...] is currently unsupported because customers reported issues when they had square brackets in their directory names.
        /// See: https://github.com/OctopusDeploy/Issues/issues/3320
        /// </remarks>
        public string? Pattern { get; }

        /// <summary>
        /// Gets or sets a value indicating an action to be performed when an error occurs during pattern matching.
        /// </summary>
        public Action<string>? ErrorLog { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that a running pattern match should be cancelled.
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether exceptions that occur during matching should be rethrown. Default is false.
        /// </summary>
        public bool ThrowOnError { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether case should be ignored in file and directory names. Default is true.
        /// </summary>
        public bool IgnoreCase { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only directories should be matched. Default is false.
        /// </summary>
        public bool DirectoriesOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="Regex" /> objects should be cached. Default is true.
        /// </summary>
        public bool CacheRegexes { get; set; }

        /// <summary>
        /// Cancels a running pattern match.
        /// </summary>
        public void Cancel()
        {
            Cancelled = true;
        }

        void Log(string s, params object[] args)
        {
            if (ErrorLog != null)
                ErrorLog(string.Format(s, args));
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <param name="pattern">The pattern to be matched.</param>
        /// <param name="ignoreCase">true if case should be ignored; false, otherwise.</param>
        /// <param name="dirOnly">true if only directories shoud be matched; false, otherwise.</param>
        /// <returns>The matched path names</returns>
        public static IEnumerable<string> ExpandNames(string pattern, bool ignoreCase = true, bool dirOnly = false)
        {
            return new Glob(pattern) { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }.ExpandNames();
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <param name="pattern">The pattern to be matched.</param>
        /// <returns>The matched <see cref="FileSystemInfo" /> objects</returns>
        public static IEnumerable<FileSystemInfo> Expand(string pattern)
        {
            return new Glob(pattern) { IgnoreCase = true, DirectoriesOnly = false }.Expand();
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <returns>The matched path names</returns>
        public IEnumerable<string> ExpandNames()
        {
            return Expand(Pattern, DirectoriesOnly).Select(f => f.FullName);
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <returns>The matched <see cref="FileSystemInfo" /> objects</returns>
        public IEnumerable<FileSystemInfo> Expand()
        {
            return Expand(Pattern, DirectoriesOnly);
        }

        RegexOrString CreateRegexOrString(string pattern)
        {
            if (!CacheRegexes)
                return new RegexOrString(GlobToRegex(pattern), pattern, IgnoreCase, false);

            RegexOrString regexOrString;

            if (!RegexOrStringCache.TryGetValue(pattern, out regexOrString))
            {
                regexOrString = new RegexOrString(GlobToRegex(pattern), pattern, IgnoreCase, true);
                RegexOrStringCache[pattern] = regexOrString;
            }

            return regexOrString;
        }

        IEnumerable<FileSystemInfo> Expand(string? path, bool dirOnly)
        {
            if (Cancelled)
                yield break;

            if (string.IsNullOrEmpty(path))
                yield break;

            // stop looking if there are no more glob characters in the path.
            // but only if ignoring case because FileSystemInfo.Exists always ignores case.
            if (IgnoreCase && path.IndexOfAny(GlobCharacters) < 0)
            {
                FileSystemInfo? fsi = null;
                var exists = false;

                try
                {
                    fsi = dirOnly || Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
                    exists = fsi.Exists;
                }
                catch (Exception ex)
                {
                    Log("Error getting FileSystemInfo for '{0}': {1}", path, ex);
                    if (ThrowOnError)
                        throw;
                }

                if (exists && fsi != null) // fsi will always be not null when exists is true, but the code analysis can't see that
                    yield return fsi;
                yield break;
            }

            string? parent;

            try
            {
                parent = Path.GetDirectoryName(path);
            }
            catch (Exception ex)
            {
                Log("Error getting directory name for '{0}': {1}", path, ex);
                if (ThrowOnError)
                    throw;
                yield break;
            }

            if (parent == null)
            {
                DirectoryInfo? dir = null;

                try
                {
                    dir = new DirectoryInfo(path);
                }
                catch (Exception ex)
                {
                    Log("Error getting DirectoryInfo for '{0}': {1}", path, ex);
                    if (ThrowOnError)
                        throw;
                }

                if (dir != null)
                    yield return dir;
                yield break;
            }

            if (parent == "")
                try
                {
                    parent = Directory.GetCurrentDirectory();
                }
                catch (Exception ex)
                {
                    Log("Error getting current working directory: {1}", ex);
                    if (ThrowOnError)
                        throw;
                }

            var child = Path.GetFileName(path);

            // handle groups that contain folders
            // child will contain unmatched closing brace
            if (child.Count(c => c == '}') > child.Count(c => c == '{'))
            {
                foreach (var group in Ungroup(path))
                foreach (var item in Expand(group, dirOnly))
                    yield return item;

                yield break;
            }

            if (child == "**")
            {
                foreach (var fileSystemInfo in Expand(parent, true).DistinctBy(d => d.FullName))
                {
                    var dir = (DirectoryInfo)fileSystemInfo;
                    DirectoryInfo[] recursiveDirectories;

                    try
                    {
                        recursiveDirectories = GetDirectories(dir).ToArray();
                    }
                    catch (Exception ex)
                    {
                        Log("Error finding recursive directory in {0}: {1}.", dir, ex);
                        if (ThrowOnError)
                            throw;
                        continue;
                    }

                    yield return dir;

                    foreach (var subDir in recursiveDirectories)
                        yield return subDir;
                }

                yield break;
            }

            var childRegexes = Ungroup(child).Select(CreateRegexOrString).ToList();

            foreach (var fileSystemInfo in Expand(parent, true).DistinctBy(d => d.FullName))
            {
                var parentDir = (DirectoryInfo)fileSystemInfo;
                IEnumerable<FileSystemInfo> fileSystemEntries;

                try
                {
                    fileSystemEntries = dirOnly ? parentDir.GetDirectories() : parentDir.GetFileSystemInfos();
                }
                catch (Exception ex)
                {
                    Log("Error finding file system entries in {0}: {1}.", parentDir, ex);
                    if (ThrowOnError)
                        throw;
                    continue;
                }

                foreach (var fileSystemEntry in fileSystemEntries)
                    if (childRegexes.Any(r => r.IsMatch(fileSystemEntry.Name)))
                        yield return fileSystemEntry;

                if (childRegexes.Any(r => r.Pattern == @"^\.\.$"))
                    yield return parentDir.Parent ?? parentDir;
                if (childRegexes.Any(r => r.Pattern == @"^\.$"))
                    yield return parentDir;
            }
        }

        static string GlobToRegex(string glob)
        {
            var regex = new StringBuilder();

            regex.Append("^");

            foreach (var c in glob)
                switch (c)
                {
                    case '*':
                        regex.Append(".*");
                        break;
                    case '?':
                        regex.Append(".");
                        break;
                    default:
                        if (RegexSpecialChars.Contains(c))
                            regex.Append('\\');
                        regex.Append(c);
                        break;
                }

            regex.Append("$");

            return regex.ToString();
        }

        static IEnumerable<string> Ungroup(string path)
        {
            if (!path.Contains('{'))
            {
                yield return path;
                yield break;
            }

            var level = 0;
            var option = "";
            var prefix = "";
            var postfix = "";
            var options = new List<string>();

            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];

                switch (c)
                {
                    case '{':
                        level++;
                        if (level == 1)
                        {
                            prefix = option;
                            option = "";
                        }
                        else
                        {
                            option += c;
                        }

                        break;
                    case ',':
                        if (level == 1)
                        {
                            options.Add(option);
                            option = "";
                        }
                        else
                        {
                            option += c;
                        }

                        break;
                    case '}':
                        level--;
                        if (level == 0)
                            options.Add(option);
                        else
                            option += c;

                        break;
                    default:
                        option += c;
                        break;
                }

                if (level == 0 && c == '}' && i + 1 < path.Length)
                {
                    postfix = path.Substring(i + 1);
                    break;
                }
            }

            if (level > 0) // invalid grouping
            {
                yield return path;
                yield break;
            }

            var postGroups = Ungroup(postfix);

            foreach (var opt in options.SelectMany(o => Ungroup(o)))
            foreach (var postGroup in postGroups)
            {
                var s = prefix + opt + postGroup;
                yield return s;
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return Pattern ?? string.Empty;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return Pattern?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        /// <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if (obj == null || GetType() != obj.GetType())
                return false;

            var g = (Glob)obj;
            return Pattern == g.Pattern;
        }

        static IEnumerable<DirectoryInfo> GetDirectories(DirectoryInfo root)
        {
            DirectoryInfo[] subDirs;

            try
            {
                subDirs = root.GetDirectories();
            }
            catch (Exception)
            {
                yield break;
            }

            foreach (var dirInfo in subDirs)
            {
                yield return dirInfo;

                foreach (var recursiveDir in GetDirectories(dirInfo))
                    yield return recursiveDir;
            }
        }

        class RegexOrString
        {
            public RegexOrString(string pattern, string rawString, bool ignoreCase, bool compileRegex)
            {
                IgnoreCase = ignoreCase;

                try
                {
                    Regex = new Regex(pattern, RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : 0) | (compileRegex ? RegexOptions.Compiled : 0));
                    Pattern = pattern;
                }
                catch
                {
                    Pattern = rawString;
                }
            }

            public Regex? Regex { get; }
            public string Pattern { get; }
            public bool IgnoreCase { get; }

            public bool IsMatch(string input)
            {
                return Regex?.IsMatch(input) ?? Pattern.Equals(input, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
        }
    }

    static class Extensions
    {
        internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            var knownKeys = new HashSet<TKey>();
            foreach (var element in source)
                if (knownKeys.Add(keySelector(element)))
                    yield return element;
        }
    }
}