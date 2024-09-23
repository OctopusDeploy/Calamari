#if NETCORE
using System;
using System.Runtime.InteropServices;
using Serilog;

namespace Calamari.Tests.KubernetesFixtures.Tools
{
    public class KindDownloader : ToolDownloader
    {
        const string LatestKindVersion = "v0.22.0";

        public KindDownloader(ILogger logger)
            : base("kind", logger)
        {
        }

        protected override string BuildDownloadUrl(Architecture processArchitecture, OperatingSystem operatingSystem)
        {
            var architecture = processArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
            var osName = GetOsName(operatingSystem);

            return $"https://github.com/kubernetes-sigs/kind/releases/download/{LatestKindVersion}/kind-{osName}-{architecture}";
        }

        static string GetOsName(OperatingSystem operatingSystem)
        {
            switch (operatingSystem)
            {
                case OperatingSystem.Windows:
                    return "windows";
                case OperatingSystem.Nix:
                    return "linux";
                case OperatingSystem.Mac:
                    return "darwin";
                default:
                    throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, null);
            }
        }
    }
}
#endif