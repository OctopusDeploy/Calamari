using System;
using System.IO;
using System.Reflection;

namespace Calamari.Extensibility.IIS.Tests.Helpers
{
    public static class TestEnvironment 
    {
        public static readonly string AssemblyLocalPath = typeof(TestEnvironment).GetTypeInfo().Assembly.FullLocalPath();
        public static readonly string CurrentWorkingDirectory = Path.GetDirectoryName(AssemblyLocalPath);
        public static readonly bool IsCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION"));

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

            public const string Mac = "macOS";
        }

        public static class ScriptingSupport
        {
            public const string FSharp = "fsharp";

            public const string ScriptCS = "scriptcs";
        }
    }
}

