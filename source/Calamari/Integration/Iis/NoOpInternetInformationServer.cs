using Calamari.FullFrameworkTools.Contracts.Iis;

namespace Calamari.Integration.Iis
{
    public class NoOpInternetInformationServer: IInternetInformationServer
    {
        public bool OverwriteHomeDirectory(string iisWebSiteName, string path, bool legacySupport)
        {
            throw new System.NotImplementedException();
        }
    }
}