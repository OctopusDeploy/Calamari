using System;

namespace Calamari.FullFrameworkTools.Iis
{
    public class CommandException : Exception
    {
        public CommandException(string message): base(message)
        {
        }
    }
    /// <summary>
    /// Command takes the form of 
    /// </summary>
    public class IisCommand : IFullFrameworkCommand
    {
        readonly IInternetInformationServer iisServer;

        public IisCommand(IInternetInformationServer iisServer)
        {
            this.iisServer = iisServer;
        }
        public string Name => "overwrite-home-directory";
        public string Execute(string[] args)
        {
            TryExtractArgs(args, 
                           out var iisWebSiteNameAndVirtualDirectory,
                           out var path, 
                           out var legacySupport);
            
            var result = iisServer.OverwriteHomeDirectory(iisWebSiteNameAndVirtualDirectory, path, legacySupport);
            return $"{{\"result\": {result.ToString().ToLower()}}}";
        }

        public string WriteHelp()
        {
            return $"Calamari.FulleFrameworkTools.exe {Name} <IisWebSiteNameAndVirtualDirectory> <Path> [<LegacySupport>]";
        }

        static void TryExtractArgs(object[] args, out string iisWebSiteNameAndVirtualDirectory, out string path, out bool legacySupport)
        {
            if (args.Length < 2)
            {
                throw new CommandException("Missing Arguments");
            }
            iisWebSiteNameAndVirtualDirectory = args[0].ToString();
            path = args[1].ToString();
            legacySupport = false;
            if (args.Length == 3)
            {
                if (!bool.TryParse(args[2].ToString(), out legacySupport))
                {
                    throw new CommandException("Invalid Argument");
                }
            }
        }
    }
}