using System;

namespace Calamari.Extensibility.IIS
{
    public abstract class WebServerSupport
    {
        public abstract void CreateWebSiteOrVirtualDirectory(string webSiteName, string virtualDirectoryPath, string webRootPath, int port);
        public abstract string GetHomeDirectory(string webSiteName, string virtualDirectoryPath);
        public abstract void DeleteWebSite(string webSiteName);
        public abstract bool ChangeHomeDirectory(string webSiteName, string virtualDirectoryPath, string newWebRootPath);

        public static WebServerSupport Legacy(ILog log)
        {
            return new WebServerSixSupport(log);            
        }

        public static WebServerSupport AutoDetect(ILog log)
        {
            // Sources:
            // http://support.microsoft.com/kb/224609
            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms724832(v=vs.85).aspx

            if (Environment.OSVersion.Version.Major < 6)
            {
                return new WebServerSixSupport(log);
            }

            return new WebServerSevenSupport();
        }
    }
}
