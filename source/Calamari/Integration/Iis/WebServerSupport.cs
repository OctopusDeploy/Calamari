using System;

namespace Calamari.Integration.Iis
{
    public abstract class WebServerSupport
    {
        public abstract void CreateWebSiteOrVirtualDirectory(string webSiteName, string virtualDirectoryPath, string webRootPath, int port);
        public abstract string GetHomeDirectory(string webSiteName, string virtualDirectoryPath);
        public abstract void DeleteWebSite(string webSiteName);
        public abstract bool ChangeHomeDirectory(string webSiteName, string virtualDirectoryPath, string newWebRootPath);
    }
}