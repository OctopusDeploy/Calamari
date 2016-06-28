namespace Calamari.Integration.Packages.NuGet
{
    internal static class NuGetServiceTypes
    {
        public static readonly string Version300beta = "/3.0.0-beta";
        public static readonly string Version300 = "/3.0.0";
        public static readonly string Version340 = "/3.4.0";

        public static readonly string[] RegistrationsBaseUrl = { "RegistrationsBaseUrl" + Version340, "RegistrationsBaseUrl" + Version300beta };
        public static readonly string PackageBaseAddress = "PackageBaseAddress" + Version300;
    }
}