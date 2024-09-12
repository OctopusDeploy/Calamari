using System;
using System.Linq;
using Calamari.FullFrameworkTools.Contracts;
using Calamari.FullFrameworkTools.Contracts.Iis;
using Calamari.Integration.Iis;

namespace Calamari.FullFrameworkTools.Iis
{
    /// <summary>
    /// Tools for working with IIS.
    /// </summary>
    public class InternetInformationServer : IInternetInformationServer
    {
        readonly ILog log;

        public InternetInformationServer(ILog log)
        {
            this.log = log;
        }
        /// <summary>
        /// Sets the home directory (web root) of the given IIS website to the given path.
        /// </summary>
        /// <param name="iisWebSiteNameAndVirtualDirectory">The name of the web site under IIS.</param>
        /// <param name="path">The path to point the site to.</param>
        /// <param name="legacySupport">If true, forces using the IIS6 compatible IIS support. Otherwise, try to auto-detect.</param>
        /// <returns>
        /// True if the IIS site was found and updated. False if it could not be found.
        /// </returns>
        public bool OverwriteHomeDirectory(string iisWebSiteNameAndVirtualDirectory, string path, bool legacySupport)
        {
            var parts = iisWebSiteNameAndVirtualDirectory.Split('/');
            var iisSiteName = parts.First();
            var remainder = parts.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            var virtualDirectory = remainder.Length > 0 ? string.Join("/", remainder) : null;

            var server = legacySupport 
                ? WebServerSupport.Legacy(log) 
                : WebServerSupport.AutoDetect(log);

            return server.ChangeHomeDirectory(iisSiteName, virtualDirectory, path);
        }
    }
}