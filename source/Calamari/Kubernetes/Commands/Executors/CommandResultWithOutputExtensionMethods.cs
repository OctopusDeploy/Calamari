using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Commands.Executors
{
    public static class CommandResultWithOutputExtensionMethods
    {
        public static void LogErrorsWithSanitizedDirectory(this CommandResultWithOutput commandResult, ILog log, string directory)
        {
            var directoryWithTrailingSlash = directory + Path.DirectorySeparatorChar;

            foreach (var message in commandResult.Output.Messages)
            {
                switch (message.Level)
                {
                    case Level.Info:
                        //No need to log as it's the output json from above.
                        break;
                    case Level.Error:
                        //Files in the error are shown with the full path in their batch directory,
                        //so we'll remove that for the user.
                        log.Error(message.Text.Replace($"{directoryWithTrailingSlash}", ""));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
            
    }
}