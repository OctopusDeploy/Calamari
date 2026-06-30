using System;

namespace Calamari.Testing.Helpers
{
    public static class TestCategory
    {
        public static class ScriptingSupport
        {
            public const string DotnetScript = "dotnet-script";
        }

        public static class CompatibleOS
        {
            public const string OnlyNix = "Nix";

            public const string OnlyWindows = "Windows";

            public const string OnlyMac = "macOS";

            public const string OnlyNixOrMac = "nixMacOS";
        }
        
        public const string PlatformAgnostic = "PlatformAgnostic";

        /// <summary>
        /// Tests that authenticate against or call a real external cloud service (e.g. real Azure).
        /// These require credentials and provisioned resources, so they run as a separate smoke suite.
        /// Tests without this category are expected to be unit/integration tests that need no external service.
        /// </summary>
        public const string ExternalCloudIntegration = "ExternalCloudIntegration";

        public const string RunOnceOnWindowsAndLinux = "RunOnceOnWindowsAndLinux";

        public const string RequiresOpenSsl1_1OrOpenSsl3 = "RequiresOpenSsl1_1OrOpenSsl3";
    }
}