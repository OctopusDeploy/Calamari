﻿#if IIS_SUPPORT
using System;

namespace Calamari.Integration.Iis
{
    public abstract class WebServerSupport
    {
        public abstract void CreateWebSiteOrVirtualDirectory(string webSiteName, string virtualDirectoryPath, string webRootPath, int port);
        public abstract string GetHomeDirectory(string webSiteName, string virtualDirectoryPath);
        public abstract void DeleteWebSite(string webSiteName);
        public abstract bool ChangeHomeDirectory(string webSiteName, string virtualDirectoryPath, string newWebRootPath);

        public static WebServerSupport Legacy()
        {
            return new WebServerSixSupport();            
        }

        public static WebServerSupport AutoDetect()
        {
            // Sources:
            // http://support.microsoft.com/kb/224609
            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms724832(v=vs.85).aspx

#pragma warning disable DE0009 // API is deprecated
            if (Environment.OSVersion.Version.Major < 6)
#pragma warning restore DE0009 // API is deprecated
            {
                return new WebServerSixSupport();
            }

            return new WebServerSevenSupport();
        }
    }
}
#endif