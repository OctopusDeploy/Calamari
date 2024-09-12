using System;
using System.IO;

namespace Calamari.FullFrameworkTools.Tests
{
    public static class TestEnvironment 
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).Assembly.FullLocalPath();
        public static readonly string CurrentWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath);

        public static readonly bool
            IsCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION"));

        public static string GetTestPath(params string[] paths)
        {
            return Path.Combine(CurrentWorkingDirectory, Path.Combine(paths));
        }

        public static string ConstructRootedPath(params string[] paths)
        {
            return Path.Combine(Path.GetPathRoot(CurrentWorkingDirectory), Path.Combine(paths));
        }
    }
}

