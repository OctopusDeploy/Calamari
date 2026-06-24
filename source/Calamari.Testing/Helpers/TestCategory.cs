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
        /// Tests that authenticate against or call a real external cloud service via SDK/HTTP (e.g. real Azure)
        /// and do NOT invoke an external CLI tool. These require credentials and provisioned resources, so they
        /// run as a separate smoke suite. Tests that shell out to a CLI binary use <see cref="ExternalToolIntegration"/>
        /// instead, even when they also hit a real cloud ("tool wins").
        /// Tests without either category are expected to be unit/integration tests that need no external service.
        /// </summary>
        public const string ExternalCloudIntegration = "ExternalCloudIntegration";

        /// <summary>
        /// Tests that invoke an external CLI tool (terraform, az, gcloud, helm, kubectl, …), whether downloaded
        /// or resolved from PATH. They need the tool present and may be slow, so they run as a separate suite.
        /// "Tool wins": a test that needs a CLI binary uses this category even if it also authenticates against
        /// a real cloud.
        /// </summary>
        public const string ExternalToolIntegration = "ExternalToolIntegration";

        public const string RunOnceOnWindowsAndLinux = "RunOnceOnWindowsAndLinux";

        public const string RequiresOpenSsl1_1OrOpenSsl3 = "RequiresOpenSsl1_1OrOpenSsl3";
    }
}