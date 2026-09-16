namespace Calamari.Util
{
    public static class HelmVersionParser
    {
        //versionString from "helm version --short"
        public static int? ParseMajorVersion(string versionString)
        {
            //eg of output for helm 2: Client: v2.16.1+gbbdfe5e
            //eg of output for helm 3: v3.0.1+g7c22ef9
            //eg of output for helm 4: v4.2.0+g0646808

            var indexOfVersionIdentifier = versionString.IndexOf('v');
            if (indexOfVersionIdentifier == -1)
                return null;

            var indexOfVersionNumber = indexOfVersionIdentifier + 1;
            if (indexOfVersionNumber >= versionString.Length)
                return null;

            var digitCount = 0;
            while (indexOfVersionNumber + digitCount < versionString.Length
                   && char.IsDigit(versionString[indexOfVersionNumber + digitCount]))
            {
                digitCount++;
            }

            if (digitCount == 0)
                return null;

            var majorVersionText = versionString.Substring(indexOfVersionNumber, digitCount);
            return int.TryParse(majorVersionText, out var majorVersion) ? majorVersion : (int?)null;
        }
    }
}
