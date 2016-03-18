using System.IO;
using Calamari.Integration.Processes;

namespace Calamari.Tests.Helpers
{
    public static class TestEnvironment
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).Assembly.FullLocalPath();
        public static readonly string CurrentWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath);

        public static string GetTestPath(params string[] paths)
        {
            return Path.Combine(CurrentWorkingDirectory, Path.Combine(paths));
        }

        public static string ConstructRootedPath(params string[] paths)
        {
            return Path.Combine(Path.GetPathRoot(CurrentWorkingDirectory), Path.Combine(paths));
        }

        public static class CompatibleOS
        {
            public const string Nix = "Nix";

            public const string Windows = "Windows";
        }

    }
}

