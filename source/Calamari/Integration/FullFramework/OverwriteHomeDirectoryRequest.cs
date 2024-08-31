using System;

namespace Calamari.Integration.FullFramework
{

    public class OverwriteHomeDirectoryRequest : IRequest
    {
        public OverwriteHomeDirectoryRequest(string iisWebSiteName, string path, bool legacySupport)
        {
            IisWebSiteName = iisWebSiteName;
            Path = path;
            LegacySupport = legacySupport;
        }

        public string IisWebSiteName { get; set; }
        public string Path { get; set; }
        public bool LegacySupport { get; set; }
    }
}