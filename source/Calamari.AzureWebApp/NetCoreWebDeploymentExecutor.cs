#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calamari.Azure.AppServices;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.AzureWebApp.Util;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    public class NetCoreWebDeploymentExecutor : IWebDeploymentExecutor
    {
        const string ToolName = "msdeploy.exe";

        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public NetCoreWebDeploymentExecutor(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public async Task ExecuteDeployment(RunningDeployment deployment, AzureTargetSite targetSite, IVariables variables, WebDeployPublishSettings publishSettings)
        {
            var retry = AzureRetryTracker.GetDefaultRetryTracker();
            var tempFolder = fileSystem.CreateTemporaryDirectory();
            while (retry.Try())
            {
                //we create a new output file for _each_ loop. Just in case the delete fails
                var outputXmlFile = $"{tempFolder}\\msdeploy_output_{Guid.NewGuid():N}.xml";
                try
                {
                    log.Verbose($"Using site '{targetSite.Site}'");
                    log.Verbose($"Using slot '{targetSite.Slot}'");

                    var msDeployArguments = BuildMsDeployArguments(
                                                                   deployment.CurrentDirectory,
                                                                   outputXmlFile,
                                                                   BuildPath(targetSite, variables),
                                                                   DestinationOptions(publishSettings),
                                                                   DeploymentSyncCommandOptions(variables));

                    var msDeployFolderPath = GetMsDeployExeFolder();
                    var msDeployExePath = Path.Combine(msDeployFolderPath, ToolName);

                    if (!fileSystem.FileExists(msDeployExePath))
                    {
                        throw new CommandException("Unable to find msdeploy.exe. The Web Deploy tooling must be installed.");
                    }

                    var commandResult = SilentProcessRunner.ExecuteCommand(msDeployExePath,
                                                                           msDeployArguments,
                                                                           msDeployFolderPath,
                                                                           _ => { },
                                                                           _ => { });

                    var changeSummary = ParseOutputXmlAndWriteTraces(outputXmlFile);

                    //if there was an error, we just blow up here as we will have written the errors in the above file
                    if (commandResult.ExitCode != 0)
                    {
                        throw new Exception(commandResult.ErrorOutput);
                    }

                    log.InfoFormat(
                                   "Successfully deployed to Azure. {0} objects added. {1} objects updated. {2} objects deleted.",
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
                finally
                {
                    //delete the output file, even on retry
                    fileSystem.DeleteFile(outputXmlFile, FailureOptions.IgnoreFailure);
                }
            }
        }

        string GetMsDeployExeFolder()
        {
            var programFiles = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");
            var msdeployFolderPath = Path.Combine("IIS", "Microsoft Web Deploy V3");

            var exeFolder = Path.Combine(programFiles, msdeployFolderPath);

            //we first look in the x86 path, if it's not there, we check in the x64 program files
            if (!fileSystem.FileExists(Path.Combine(exeFolder, ToolName)))
            {
                // On 32-bit Operating Systems, this will return C:\Program Files
                // On 64-bit Operating Systems - regardless of process bitness, this will return C:\Program Files
                if (!Environment.Is64BitOperatingSystem || Environment.Is64BitProcess)
                {
                    programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                }
                else
                {
                    // 32 bit process on a 64 bit OS can't use SpecialFolder.ProgramFiles to get the 64-bit program files folder
                    programFiles = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
                }

                //update the exeFolder with the new program files
                exeFolder = Path.Combine(programFiles, msdeployFolderPath);
            }

            return exeFolder;
        }

        DeploymentChangeSummary ParseOutputXmlAndWriteTraces(string outputXmlFile)
        {
            using var fileStream = fileSystem.OpenFile(outputXmlFile, FileAccess.Read);
            {
                var xDocument = XDocument.Load(fileStream, LoadOptions.None);

                var outputElement = xDocument.Element("output");
                if (outputElement is null)
                {
                    throw new InvalidOperationException("There was no output xml node");
                }

                //find and log all trace events
                var traceEvents = outputElement.Elements("traceEvent")
                                               .Where(x => x.Attribute("type")?.Value == "Microsoft.Web.Deployment.DeploymentAgentTraceEvent")
                                               .Select(x => (Level: x.Attribute("eventLevel")?.Value, Message: x.Attribute("message")?.Value));

                foreach (var (level, message) in traceEvents)
                {
                    switch (level)
                    {
                        case "Verbose":
                            log.Verbose(message);
                            break;
                        case "Info":
                            // The deploy-log is noisy; we'll log info as verbose
                            log.Verbose(message);
                            break;
                        case "Warning":
                            log.Warn(message);
                            break;
                        case "Error":
                            log.Error(message);
                            break;
                    }
                }

                return DeploymentChangeSummary.FromXElement(outputElement.Element("syncResults"));
            }
        }

        static string BuildMsDeployArguments(string currentDirectory,
                                             string outputXmlFile,
                                             string path,
                                             IEnumerable<string> destinationOptions,
                                             IEnumerable<string> syncOptions)
        {
            var parts = new List<string>
            {
                //verb
                "-verb:sync",
                //source
                $"-source:contentPath={currentDirectory}",
                //destination
                $"-dest:contentPath={path},{string.Join(",", destinationOptions)}",
                //we write the output to XML for later parsing
                $"-xml:{outputXmlFile}"
            };

            parts.AddRange(syncOptions);

            return string.Join(" ", parts);
        }

        static IEnumerable<string> DestinationOptions(WebDeployPublishSettings settings)
        {
            var publishProfile = settings.PublishProfile;
            var deploySite = settings.DeploymentSite;

            var url = new Uri(publishProfile.Uri, $"/msdeploy.axd?site={deploySite}").ToString();

            return new[]
            {
                "AuthType=Basic",
                $"UserName={publishProfile.UserName}",
                $"Password={publishProfile.Password}",
                $"ComputerName={url}"
            };
        }

        static IEnumerable<string> DeploymentSyncCommandOptions(IVariables variables)
        {
            var args = new List<string>
            {
                "-userAgent:OctopusDeploy/1.0",
                "-retryAttempts:3",
                "-retryInterval:1000",
                "-verbose"
            };

            if (variables.GetFlag(SpecialVariables.Action.Azure.UseChecksum))
            {
                args.Add("-useChecksum");
            }

            if (!variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles))
            {
                args.Add("-enableRule:DoNotDeleteRule");
            }

            if (variables.GetFlag(SpecialVariables.Action.Azure.AppOffline))
            {
                //TODO: How do we know if the Azure Deployment API does not support 'AppOffline' (which is something that can be true)?
                args.Add("-enableRule:AppOffline");
            }

            // If PreservePaths variable set, then create SkipDelete rules for each path regex
            var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');
            foreach (var path in preservePaths)
            {
                args.Add($"-skip:skipAction=Delete,objectName=filePath,absolutePath={path}");
                args.Add($"-skip:skipAction=Delete,objectName=dirPath,absolutePath={path}");
            }

            // If PreserveAppData variable set, then create SkipDelete rules for App_Data directory
            // ReSharper disable once InvertIf
            if (variables.GetFlag(SpecialVariables.Action.Azure.PreserveAppData))
            {
                args.Add(@"-skip:skipAction=Delete,objectName=filePath,absolutePath=\\App_Data\\.*");
                args.Add(@"-skip:skipAction=Delete,objectName=dirPath,absolutePath=\\App_Data(\\.*|$)");
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

        class DeploymentChangeSummary
        {
            DeploymentChangeSummary(int objectsAdded, int objectsDeleted, int objectsUpdated)
            {
                ObjectsAdded = objectsAdded;
                ObjectsDeleted = objectsDeleted;
                ObjectsUpdated = objectsUpdated;
            }

            public int ObjectsAdded { get; }
            public int ObjectsDeleted { get; }
            public int ObjectsUpdated { get; }

            public static DeploymentChangeSummary FromXElement(XElement element)
                => new DeploymentChangeSummary(
                                               int.TryParse(element?.Attribute("objectsAdded")?.Value, out var added) ? added : -1,
                                               int.TryParse(element?.Attribute("objectsDeleted")?.Value, out var deleted) ? deleted : -1,
                                               int.TryParse(element?.Attribute("objectsUpdated")?.Value, out var updated) ? updated : -1);
        }
    }
}
#endif