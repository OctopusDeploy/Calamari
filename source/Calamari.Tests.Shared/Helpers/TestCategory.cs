using System;

namespace Calamari.Tests.Shared.Helpers
{
    public static class TestCategory
    {
        public const string PlatformAgnostic = "PlatformAgnostic";

        public static class ScriptingSupport
        {
            public const string FSharp = "fsharp";

            public const string ScriptCS = "scriptcs";
        }

        public static class CompatibleOS
        {
            public const string OnlyNix = "Nix";

            public const string OnlyWindows = "Windows";

            public const string OnlyMac = "macOS";

            public const string OnlyNixOrMac = "nixMacOS";
        }
    }
}