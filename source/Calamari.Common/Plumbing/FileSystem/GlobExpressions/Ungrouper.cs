using System.Collections.Generic;
using System.Linq;

namespace Calamari.Common.Plumbing.FileSystem.GlobExpressions
{
    public static class Ungrouper
    {
        public static IEnumerable<string> UngroupPath(string path)
        {
            var groups = new List<Group>();
            if (path.Contains('{'))
            {
                groups.AddRange(StringGroupRetriever.GetStringGroups(path));
            }

            if (path.Contains('['))
            {
                groups.AddRange(CharGroupRetriever.GetCharGroups(path));
            }

            if (!groups.Any())
                return new string[] { path };

            var orderedGroups = groups.OrderByDescending(g => g.StartIndex);
            var expandedPaths = new List<string>{path};
            foreach (var group in orderedGroups)
            {
                var newPaths = new List<string>();
                foreach (var p in expandedPaths)
                {
                    var newPath = p.Remove(group.StartIndex, group.Length);
                    foreach (var option in group.Options)
                    {
                        newPaths.Add(newPath.Insert(group.StartIndex, option));
                    }

                    if (group is CharGroup { IsRange: false })
                    {
                        newPaths.Add(p);
                    }
                }

                expandedPaths = newPaths;
            }

            return expandedPaths;
        }
    }
}