using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Common.Plumbing.FileSystem.GlobExpressions
{
    public static class CharGroupRetriever
    {
        private const char RangeIdentifier = '-';
        public const char GroupStart = '[';
        public const char GroupEnd = ']';
        public static IEnumerable<Group> GetCharGroups(string path)
        {
            var inGroup = false;
            var isRange = false;
            var groupEndIndex = 0;
            var options = new Stack<char>();
            var groups = new List<Group>();

            void Reset()
            {
                inGroup = false;
                isRange = false;
                groupEndIndex = 0;
                options.Clear();
            }

            for (var index = path.Length - 1; index >= 0; index--)
            {
                var c = path[index];
                if (c == Path.DirectorySeparatorChar ||
                    c == Path.AltDirectorySeparatorChar)
                {
                    Reset();
                    continue;
                }

                switch (c)
                {
                    case GroupEnd:
                        options.Clear();
                        inGroup = true;
                        groupEndIndex = index;
                        break;
                    case GroupStart:
                        if (!inGroup) break;
                        if (options.Count < 2) break;
                        if (isRange)
                        {
                            void ThrowException()
                            {
                                throw new InvalidOperationException(
                                    "A [a-b] Glob Expression group must contain two chars separated by a " +
                                    $"'{RangeIdentifier}' where the first char's value is less than the second char.");
                            }

                            if (options.Count != 3)
                                ThrowException();

                            var lower = options.First();
                            var higher = options.Last();

                            if (lower < higher &&
                                lower != RangeIdentifier &&
                                higher != RangeIdentifier)
                            {
                                var opts = new List<string>();
                                for (var character = options.First(); character <= options.Last(); character++)
                                {
                                    opts.Add(character.ToString());
                                }

                                groups.Add(new Group(index, groupEndIndex + 1 - index, opts.ToArray()));

                                Reset();
                                break;
                            }

                            ThrowException();
                        }

                        var groupOptions = GetGroupOptions(options);
                        groups.Add(new Group(index, groupEndIndex + 1 - index, groupOptions));

                        Reset();
                        break;
                    case RangeIdentifier:
                        if (inGroup)
                        {
                            isRange = true;
                            options.Push('-');
                        }
                        break;
                    case StringGroupRetriever.GroupStart:
                    case StringGroupRetriever.GroupEnd:
                        Reset();
                        break;
                    default:
                        if (inGroup)
                        {
                            options.Push(c);
                        }
                        break;
                }
            }
            return groups;
        }

        /// <remarks>
        /// To allow us to have Glob Expression group support and maintain backward compatibility
        /// we return all options found within a group (eg: [abc] => 'a','b','c') as well as the group
        /// itself as a literal (eg: [abc] => '[abc]') this means users targeting a path with square
        /// brackets in it, will continue to work correctly.
        /// </remarks>
        private static string[] GetGroupOptions(Stack<char> options)
        {
            var groupOptions = options.Select(o => o.ToString());
            // The next line is the workaround
            groupOptions = groupOptions.Concat(new[] { $"{GroupStart}{string.Concat(options)}{GroupEnd}" });

            return groupOptions.ToArray();
        }
    }
}