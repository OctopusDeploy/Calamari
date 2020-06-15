using System.IO;
using Calamari;

namespace Sashimi.Tests.Shared
{
    public static class TestEnvironment 
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).Assembly.FullLocalPath();
        public static readonly string CurrentWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath)!;

        public static string GetTestPath(params string[] paths)
        {
            return Path.Combine(CurrentWorkingDirectory, Path.Combine(paths));
        }
    }
}