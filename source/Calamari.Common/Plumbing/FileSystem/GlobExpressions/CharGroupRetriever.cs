using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Common.Plumbing.FileSystem.GlobExpressions
{
    public static class CharGroupRetriever
    {
        public static IEnumerable<Group> GetCharGroups(string path)
        {
            var inGroup = false;
            var isRange = false;
            var groupEndIndex = 0;
            var options = new List<char>();
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
                if (c == Path.DirectorySeparatorChar)
                {
                    Reset();
                    continue;
                }

                switch (c)
                {
                    case ']':
                        options.Clear();
                        inGroup = true;
                        groupEndIndex = index;
                        break;
                    case '[':
                        if (!inGroup) break;
                        if (options.Count < 2) break;
                        if (isRange)
                        {
                            // Note: options are collected in reverse order so the
                            // left-hand side of [a-b] is last in the options list.
                            if (options.Count == 2 &&
                                options[1] < options[0])
                            {
                                var opts = new List<string>();
                                for (var character = options[1]; character <= options[0]; character++)
                                {
                                    opts.Add(character.ToString());
                                }

                                groups.Add(new CharGroup(index, groupEndIndex + 1 - index, opts.ToArray(), true));
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    "A [a-b] Glob Expression group must contain two chars separated by a '-' " +
                                    "where the first char's value is less than the second char.");
                            }
                        }
                        else
                        {
                            groups.Add(new CharGroup(index, groupEndIndex + 1 - index,
                                options.Select(o => o.ToString()).ToArray(), false));
                        }
                        Reset();
                        break;
                    case '-':
                        if (inGroup)
                        {
                            isRange = true;
                        }
                        break;
                    case '{':
                    case '}':
                        Reset();
                        break;
                    default:
                        if (inGroup)
                        {
                            options.Add(c);
                        }
                        break;
                }
            }
            return groups;
        }
    }
}