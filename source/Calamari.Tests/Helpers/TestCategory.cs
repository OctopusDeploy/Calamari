namespace Calamari.Tests.Helpers
{
    public static class TestCategory
    {
        public static class ScriptingSupport
        {
            public const string FSharp = "fsharp";

            public const string ScriptCS = "scriptcs";
        }

        public static class CompatibleOS
        {
            public const string Nix = "Nix";

            public const string Windows = "Windows";

            public const string Mac = "macOS";
        }
        
        public const string PlatformAgnostic = "PlatformAgnostic";

        public static class WindowsEdition
        {
            public const string PowerShellCore = "PowerShellCore";

            public const string PowerShell = "PowerShell";
        }
    }
}