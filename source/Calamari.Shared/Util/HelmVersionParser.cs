using System;

namespace Calamari.Util
{
    public enum HelmVersion
    {
        V2,
        V3
    }

    public static class HelmVersionParser
    {
        //versionString from "helm version --client --short"
        public static HelmVersion ParseVersion(string versionString)
        {
            //eg of output for helm 2: Client: v2.16.1+gbbdfe5e
            //eg of output for helm 3: v3.0.1+g7c22ef9
            
            var indexOfVersionIdentifier = versionString.IndexOf('v');
            if (indexOfVersionIdentifier == -1)
                throw new FormatException($"Failed to find version identifier from '{versionString}'.");

            var indexOfVersionNumber = indexOfVersionIdentifier + 1;
            if (indexOfVersionNumber >= versionString.Length)
                throw new FormatException($"Failed to find version number from '{versionString}'.");

            var version = versionString[indexOfVersionNumber];
            switch (version)
            {
                case '3':
                    return HelmVersion.V3;
                case '2':
                    return HelmVersion.V2;
                default:
                    throw new InvalidOperationException($"Unsupported helm version '{version}'");
            }
        }
    }
}