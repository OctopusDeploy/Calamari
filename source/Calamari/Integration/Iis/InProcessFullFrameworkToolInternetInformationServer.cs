#if IIS_SUPPORT
using System;
using Calamari.FullFrameworkTools.Command;

namespace Calamari.Integration.Iis
{
    public class InProcessFullFrameworkToolInternetInformationServer: IInternetInformationServer
    {
        public bool OverwriteHomeDirectory(string iisWebSiteName, string path, bool legacySupport)
        {
            var commandLocator = new CommandLocator();
            var cmd = commandLocator.GetCommand<OverwriteHomeDirectoryHandler>();
            var result = (OverwriteHomeDirectoryResponse)cmd.Handle(new OverwriteHomeDirectoryRequest()
            {
                Path = path,
                IisWebSiteName = iisWebSiteName,
                LegacySupport = legacySupport
            });
            return result.Result;
        }
    }
}
#endif