using System;
using System.IO;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class PathExtensions
    {
        public static bool IsChildOf(this string child, string parent)
        {
            var childDir = GetSanitizedDirInfo(child);
            var parentDir = GetSanitizedDirInfo(parent);
            var isParent = false;
            if (childDir.FullName == parentDir.FullName)
                return true;
            while (childDir.Parent != null)
            {
                if (childDir.Parent.FullName == parentDir.FullName)
                {
                    isParent = true;
                    break;
                }

                childDir = childDir.Parent;
            }

            return isParent;
        }

        static DirectoryInfo GetSanitizedDirInfo(string dir)
        {
            if (dir == "/")
                return new DirectoryInfo(dir);

            dir = dir.TrimEnd('\\', '/'); // normal paths need trailing path separator removed to match
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                if (dir.EndsWith(":")) // c: needs trailing slash to match
                    dir = dir + "\\";
                dir = dir.ToLowerInvariant();
            }

            return new DirectoryInfo(dir);
        }
    }
}