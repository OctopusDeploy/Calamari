using System.Collections.Generic;
using System.IO;

namespace Calamari.Common.Plumbing.FileSystem.GlobExpressions
{
    public static class StringGroupRetriever
    {
        private const char OptionSeparator = ',';
        public const char GroupStart = '{';
        public const char GroupEnd = '}';

        public static IEnumerable<Group> GetStringGroups(string path)
        {
            var inGroup = false;
            var groupEndIndex = 0;
            var option = new Stack<char>();
            var options = new List<string>();
            var groups = new List<Group>();

            void Reset()
            {
                inGroup = false;
                groupEndIndex = 0;
                options.Clear();
                option.Clear();
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
                        Reset();
                        inGroup = true;
                        groupEndIndex = index;
                        break;
                    case GroupStart:
                        if (!inGroup) break;
                        if (option.Count > 0)
                            options.Add(new string(option.ToArray()));

                        if (options.Count < 2) break;
                        groups.Add(new Group(index, groupEndIndex + 1 - index, options.ToArray()));
                        Reset();
                        break;
                    case OptionSeparator:
                        if (inGroup)
                        {
                            options.Add(new string(option.ToArray()));
                            option.Clear();
                        }
                        break;
                    case CharGroupRetriever.GroupStart:
                    case CharGroupRetriever.GroupEnd:
                        Reset();
                        break;
                    default:
                        if (inGroup)
                        {
                            option.Push(c);
                        }

                        break;
                }
            }

            return groups;
        }
    }
}