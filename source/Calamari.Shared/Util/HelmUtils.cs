using System.IO;
using Calamari.Commands.Support;
using SharpCompress.Common;

namespace Calamari.Util
{
    public static class HelmUtils
    {
        public enum HelmVersion
        {
            Version2,
            Version3
        }

        public static HelmVersion ParseHelmVersionFromHelmVersionCmdOutput(string versionOutput)
        {
            if (versionOutput.IndexOf('v') < 0)
            {
                throw new InvalidFormatException(
                    $"Version output of {versionOutput} cannot be parsed into helm version as it's of invalid format");
            }
            var version = versionOutput[versionOutput.IndexOf('v') + 1];

            return version.Equals('3') ? HelmUtils.HelmVersion.Version3 : HelmUtils.HelmVersion.Version2;
        }
    }
}