namespace Calamari.Integration.Iis
{
    public interface IInternetInformationServer
    {
        /// <summary>
        /// Sets the home directory (web root) of the given IIS website to the given path.
        /// </summary>
        /// <param name="iisWebSiteName">The name of the web site under IIS.</param>
        /// <param name="path">The path to point the site to.</param>
        /// <param name="legacySupport">If true, forces using the IIS6 compatible IIS support. Otherwise, try to auto-detect.</param>
        /// <returns>True if the IIS site was found and updated. False if it could not be found.</returns>
        bool OverwriteHomeDirectory(string iisWebSiteName, string path, bool legacySupport);
    }
}