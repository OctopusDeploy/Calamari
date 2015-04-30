using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.Processes;

namespace Calamari.Tests.Helpers
{
    public static class TestEnvironment
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).Assembly.FullLocalPath();
        public static readonly string CurrentWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath);

        public static string SolutionRoot
        {
            get
            {
//                Console.WriteLine("AssemblyLocalPath " + AssemblyLocalPath);
//                string targetFolder = "source" + Path.DirectorySeparatorChar;
//                int index = AssemblyLocalPath.LastIndexOf(targetFolder, StringComparison.OrdinalIgnoreCase);
//                var ss =AssemblyLocalPath.Substring(0, index + targetFolder.Length - 1);
//                Console.WriteLine("SolutionRoot " + ss);
                return CurrentWorkingDirectory;
            }
        }

        public static class CompatableOS
        {
            public const string Nix = "Nix";

            public const string Windows = "Windows";

            public const string All = "All";
        }

        public static string GetTestPath(params string[] paths)
        {
            return Path.Combine(CurrentWorkingDirectory, Path.Combine(paths));
        }

        public static string ConstructRootedPath(params string[] paths)
        {
            return Path.Combine(Path.GetPathRoot(SolutionRoot), Path.Combine(paths));
        }
    }
}

