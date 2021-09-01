using System;

namespace Sashimi.Server.Contracts.Actions
{
    public class KnownPlatforms
    {
        public static readonly string Windows = "win-x64";
        public static readonly string WindowsNetFx = "netfx";
        public static readonly string Linux64 = "linux-x64";
        public static readonly string LinuxArm = "linux-arm";
        public static readonly string LinuxArm64 = "linux-arm64";
        public static readonly string Osx64 = "osx-x64";
        public static readonly string Mono = "mono";

        public static readonly string[] All = { Windows, Linux64, LinuxArm, LinuxArm64, Osx64, Mono };
    }
}