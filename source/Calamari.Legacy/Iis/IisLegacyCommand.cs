using System;
using System.Configuration;

namespace Calamari.Legacy.Iis
{
    public class IisLegacyCommand : ILegacyCommand
    {
        readonly IInternetInformationServer iisServer;

        public IisLegacyCommand(IInternetInformationServer iisServer)
        {
            this.iisServer = iisServer;
        }
        public string Name => "overwrite-home-directory";
        public void Execute(string[] args)
        {
            TryExtractArgs(args, 
                           out var iisWebSiteNameAndVirtualDirectory,
                           out var path, 
                           out var legacySupport);
            
            iisServer.OverwriteHomeDirectory(iisWebSiteNameAndVirtualDirectory, path, legacySupport);
        }

        static void TryExtractArgs(object[] args, out string iisWebSiteNameAndVirtualDirectory, out string path, out bool legacySupport)
        {
            if (args.Length < 2)
            {
                throw new InvalidOperationException("Missing Arguments");
            }
            iisWebSiteNameAndVirtualDirectory = args[0].ToString();
            path = args[1].ToString();
            legacySupport = false;
            if (args.Length == 3)
            {
                if (!bool.TryParse(args[3].ToString(), out legacySupport))
                {
                    throw new InvalidOperationException("Invalid Argument");
                }
            }
        }
    }
}