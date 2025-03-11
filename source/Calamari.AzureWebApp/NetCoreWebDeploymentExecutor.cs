#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Azure.AppServices;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.AzureWebApp.Util;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.AzureWebApp
{
    public class NetCoreWebDeploymentExecutor : IWebDeploymentExecutor
    {
        const string ToolName = "Calamari.AzureWebApp.NetCoreShim.exe";

        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public NetCoreWebDeploymentExecutor(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public async Task ExecuteDeployment(RunningDeployment deployment, AzureTargetSite targetSite, IVariables variables, WebDeployPublishSettings publishSettings)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new CommandException("Cannot execute on non-Windows operating systems as there is a required dependency on the Web Deploy tooling (msdeploy.exe).");
            }

            var netCoreShimExeFolder = GetNetCoreShimExeFolder();
            var netCoreShimExePath = Path.Combine(netCoreShimExeFolder, ToolName);

            if (!fileSystem.FileExists(netCoreShimExePath))
            {
                throw new CommandException($"Unable to find {netCoreShimExePath}");
            }

            var retry = AzureRetryTracker.GetDefaultRetryTracker();
            while (retry.Try())
            {
                try
                {
                    log.Verbose($"Using site '{targetSite.Site}'");
                    log.Verbose($"Using slot '{targetSite.Slot}'");

                    var netCoreShimArguments = BuildNetCoreShimArguments(
                                                                         deployment.CurrentDirectory,
                                                                         BuildPath(targetSite, variables),
                                                                         DestinationOptions(publishSettings),
                                                                         DeploymentSyncCommandOptions(variables));

                    string resultMessage = null; 

                    log.Verbose($"Executing {netCoreShimExePath}");
                    var commandResult = SilentProcessRunner.ExecuteCommand(netCoreShimExePath,
                                                                           netCoreShimArguments,
                                                                           netCoreShimExeFolder,
                                                                           s =>
                                                                           {
                                                                               //if this is the result message
                                                                               if (s.StartsWith("INF|RESULT|"))
                                                                               {
                                                                                   resultMessage = s;
                                                                               }
                                                                               LogOutputMessage(s);
                                                                           },
                                                                           LogOutputMessage);


                    //if there was an error, we just blow up here as we will have written the errors in the above file
                    if (commandResult.ExitCode != 0)
                    {
                        throw new Exception(commandResult.ErrorOutput);
                    }

                    if (resultMessage == null)
                    {
                        log.Warn("Successfully deployed to Azure but was unable to determine the objects that changed.");
                        break;
                    }

                    var changeSummaryJson = resultMessage.Split("|").Last();
                    var changeSummary = JsonConvert.DeserializeAnonymousType(changeSummaryJson,
                                                                             new
                                                                             {
                                                                                 ObjectsAdded = 0,
                                                                                 ObjectsUpdated = 0,
                                                                                 ObjectsDeleted = 0
                                                                             });

                    log.InfoFormat("Successfully deployed to Azure. {0} objects added. {1} objects updated. {2} objects deleted.",
                                   changeSummary.ObjectsAdded,
                                   changeSummary.ObjectsUpdated,
                                   changeSummary.ObjectsDeleted);
                    break;
                }
                catch (Exception ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            Log.VerboseFormat("Retry #{0} on Azure deploy. Exception: {1}",
                                              retry.CurrentTry,
                                              ex.Message);
                        }

                        await Task.Delay(retry.Sleep());
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        void LogOutputMessage(string msg)
        {
            var firstIndex = msg.IndexOf("|", StringComparison.Ordinal);
            var level = msg[..firstIndex];
            var message = msg[(firstIndex + 1)..];

            //if this is the result message, do nothing
            if (message.StartsWith("RESULT|"))
            {
                return;
            }

            switch (level)
            {
                case "VRB":
                case "DBG":
                    log.Verbose(message);
                    break;
                case "INF":
                    log.Info(message);
                    break;
                case "WRN":
                    log.Warn(message);
                    break;
                case "ERR":
                case "FTL":
                    log.Error(message);
                    break;
            }
        }

        static string GetNetCoreShimExeFolder()
        {
            var myPath = typeof(NetCoreWebDeploymentExecutor).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);
            return Path.GetFullPath(Path.Combine(parent, "netcoreshim"));
        }

        static string BuildNetCoreShimArguments(string currentDirectory,
                                                string path,
                                                IEnumerable<string> destinationOptions,
                                                IEnumerable<string> syncOptions)
        {
            var parts = new List<string>
            {
                "sync",
                $"--sourcePath=\"{currentDirectory}\"",
                $"--destPath=\"{path}\"",
            };

            parts.AddRange(destinationOptions);

            parts.AddRange(syncOptions);

            return string.Join(" ", parts);
        }

        static IEnumerable<string> DestinationOptions(WebDeployPublishSettings settings)
        {
            var publishProfile = settings.PublishProfile;

            var encryptionKey = AesEncryption.RandomString(16);
            var encryptor = AesEncryption.ForServerVariables(encryptionKey);

            //we enc
            var encryptedUserName = Convert.ToBase64String(encryptor.Encrypt(publishProfile.UserName));
            var encryptedPassword = Convert.ToBase64String(encryptor.Encrypt(publishProfile.Password));

            return new[]
            {
                $"--destUserName=\"{encryptedUserName}\"",
                $"--destPassword=\"{encryptedPassword}\"",
                $"--destUri=\"{publishProfile.Uri}\"",
                $"--destSite=\"{settings.DeploymentSite}\"",
                $"--encryptionKey=\"{encryptionKey}\""
            };
        }

        static IEnumerable<string> DeploymentSyncCommandOptions(IVariables variables)
        {
            var args = new List<string>();

            if (variables.GetFlag(SpecialVariables.Action.Azure.UseChecksum))
            {
                args.Add("--useChecksum");
            }

            if (!variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles))
            {
                args.Add("--doNotDelete");
            }

            if (variables.GetFlag(SpecialVariables.Action.Azure.AppOffline))
            {
                //TODO: How do we know if the Azure Deployment API does not support 'AppOffline' (which is something that can be true)?
                args.Add("--useAppOffline");
            }

            var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');
            if (preservePaths.Count > 0)
            {
                args.Add($"--preservePaths={string.Join("|",preservePaths.Select(s => $"\"{s}\""))}");
            }

            // ReSharper disable once InvertIf
            if (variables.GetFlag(SpecialVariables.Action.Azure.PreserveAppData))
            {
                args.Add("--preserveAppData");
            }

            return args;
        }

        static string BuildPath(AzureTargetSite site, IVariables variables)
        {
            var relativePath = variables.Get(SpecialVariables.Action.Azure.PhysicalPath, string.Empty)?.TrimStart('\\');
            return !string.IsNullOrWhiteSpace(relativePath)
                ? site.Site + "\\" + relativePath
                : site.Site;
        }
    }
}
#endif