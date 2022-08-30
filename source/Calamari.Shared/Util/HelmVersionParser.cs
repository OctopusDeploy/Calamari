namespace Calamari.Util
{
    public static class HelmVersionParser
    {
        //versionString from "helm version --client --short"
        public static HelmVersion? ParseVersion(string versionString)
        {
            //eg of output for helm 2: Client: v2.16.1+gbbdfe5e
            //eg of output for helm 3: v3.0.1+g7c22ef9
            
            var indexOfVersionIdentifier = versionString.IndexOf('v');
            if (indexOfVersionIdentifier == -1)
                return null;

            var indexOfVersionNumber = indexOfVersionIdentifier + 1;
            if (indexOfVersionNumber >= versionString.Length)
                return null;

            var version = versionString[indexOfVersionNumber];
            switch (version)
            {
                case '3':
                    return HelmVersion.V3;
                case '2':
                    return HelmVersion.V2;
                default:
                    return null;
            }
        }
    }
}