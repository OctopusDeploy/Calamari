using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeWithKOSConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;
        readonly HelmTemplateValueSourcesParser valueSourcesParser;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly IManifestReporter manifestReporter;
        readonly Kubectl kubectl;

        public HelmUpgradeWithKOSConvention(ILog log,
                                            ICommandLineRunner commandLineRunner,
                                            ICalamariFileSystem fileSystem,
                                            HelmTemplateValueSourcesParser valueSourcesParser,
                                            IResourceStatusReportExecutor statusReporter,
                                            IManifestReporter manifestReporter,
                                            Kubectl kubectl)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.valueSourcesParser = valueSourcesParser;
            this.statusReporter = statusReporter;
            this.manifestReporter = manifestReporter;
            this.kubectl = kubectl;
        }

        public void Install(RunningDeployment deployment)
        {
            var waitForJobs = deployment.Variables.GetFlag(SpecialVariables.WaitForJobs);
            var isKOSEnabled = deployment.Variables.GetFlag(SpecialVariables.ResourceStatusCheck);

            var releaseName = GetReleaseName(deployment.Variables);

            var helmCli = new HelmCli(log, commandLineRunner, deployment);

            kubectl.SetKubectl();

            var currentRevisionNumber = helmCli.GetCurrentRevision(releaseName);

            var newRevisionNumber = (currentRevisionNumber ?? 0) + 1;

            //This is used to cancel KOS when the helm upgrade has completed
            //It does not cancel the get manifest
            var kosCts = new CancellationTokenSource();

            var helmUpgradeTask = Task.Run(() => ExecuteHelmUpgrade(deployment,
                                                                    helmCli,
                                                                    releaseName,
                                                                    isKOSEnabled,
                                                                    waitForJobs,
                                                                    kosCts));
            
            var manifestAndStatusCheckTask = Task.Run(async () =>
                                                      {
                                                          var manifest = await PollForManifest(helmCli, releaseName, newRevisionNumber);

                                                          //report the manifest has been applied
                                                          manifestReporter.ReportManifestApplied(manifest);

                                                          //if resource status (KOS) is enabled, parse the manifest and start monitored the resources
                                                          if (isKOSEnabled)
                                                          {
                                                              await ParseManifestAndMonitorResourceStatuses(deployment, manifest, kosCts.Token);
                                                          }
                                                      },
                                                      kosCts.Token);

            //we run both the helm upgrade and the manifest & status in parallel
            Task.WhenAll(helmUpgradeTask, manifestAndStatusCheckTask).GetAwaiter().GetResult();
        }

        async Task<string> PollForManifest(HelmCli helmCli,
                                           string releaseName,
                                           int revisionNumber)
        {
            var ct = new CancellationTokenSource();
            ct.CancelAfter(TimeSpan.FromMinutes(10));
            string manifest = null;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    manifest = helmCli.GetManifest(releaseName, revisionNumber);
                    log.Verbose($"Retrieved manifest for {releaseName}, revision {revisionNumber}.");
                    break;
                }
                catch (CommandLineException)
                {
                    log.Verbose("Helm manifest was not ready for retrieval. Retrying in 1s.");
                    await Task.Delay(TimeSpan.FromSeconds(1), ct.Token);
                }
            }

            if (string.IsNullOrWhiteSpace(manifest))
            {
                throw new CommandException("Failed to retrieve helm manifest in a timely manner");
            }

            return manifest;
        }

        async Task ParseManifestAndMonitorResourceStatuses(RunningDeployment deployment, string manifest, CancellationToken cancellationToken)
        {
            var namespacedApiResourceDict = GetNamespacedApiResourceDictionary();

            var resources = new List<ResourceIdentifier>();
            using (var reader = new StringReader(manifest))
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(reader);

                foreach (var document in yamlStream.Documents)
                {
                    if (!(document.RootNode is YamlMappingNode rootNode))
                    {
                        log.Warn("Could not parse manifest, resources will not be added to kubernetes object status");
                        continue;
                    }

                    var kind = rootNode.GetChildNode<YamlScalarNode>("kind").Value;

                    var metadataNode = rootNode.GetChildNode<YamlMappingNode>("metadata");
                    var name = metadataNode.GetChildNode<YamlScalarNode>("name").Value;
                    var @namespace = metadataNode.GetChildNodeIfExists<YamlScalarNode>("namespace")?.Value;

                    var apiResourceIdentifier = GetApiResourceIdentifier(rootNode);

                    // If we can't find the resource in the dictionary, we assume it is namespaced
                    // Otherwise, we use the value in the dictionary
                    var isNamespaced = !namespacedApiResourceDict.TryGetValue(apiResourceIdentifier, out var isNamespacedValue) | isNamespacedValue;

                    if (isNamespaced && string.IsNullOrWhiteSpace(@namespace))
                    {
                        @namespace = deployment.Variables.Get(SpecialVariables.Helm.Namespace);
                        //if we don't have a custom helm namespace
                        if (string.IsNullOrWhiteSpace(@namespace))
                        {
                            //use the defined namespace
                            @namespace = deployment.Variables.Get(SpecialVariables.Namespace);
                        }
                    }

                    var resourceIdentifier = new ResourceIdentifier(kind, name, @namespace);
                    resources.Add(resourceIdentifier);
                }
            }

            //We are using helm as the deployment verification so an infinite timeout and wait for jobs makes sense
            var statusCheck = statusReporter.Start(0, false, resources);
            await statusCheck.WaitForCompletionOrTimeout(cancellationToken);
        }

        void ExecuteHelmUpgrade(RunningDeployment deployment,
                                HelmCli helmCli,
                                string releaseName,
                                bool isKOSEnabled,
                                bool waitForJobs,
                                CancellationTokenSource cts)
        {
            var packagePath = GetChartLocation(deployment);

            var args = GetUpgradeCommandArgs(deployment,
                                             helmCli,
                                             isKOSEnabled,
                                             waitForJobs);

            var result = helmCli.Upgrade(releaseName, packagePath, args);

            if (result.ExitCode != 0)
            {
                throw new CommandException($"Helm Upgrade returned non-zero exit code: {result.ExitCode}. Deployment terminated.");
            }

            if (result.HasErrors && deployment.Variables.GetFlag(Deployment.SpecialVariables.Action.FailScriptOnErrorOutput))
            {
                throw new CommandException("Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
            }
            
            //once the helm command has finished, cancel KOS
            cts.Cancel();
        }

        List<string> GetUpgradeCommandArgs(RunningDeployment deployment,
                                           HelmCli helmCli,
                                           bool isKOSEnabled,
                                           bool waitForJobs
        )
        {
            var args = new List<string>();

            var helmVersion = GetVersion(deployment.Variables);
            CheckHelmToolVersion(helmCli, helmVersion);

            if (helmVersion == HelmVersion.V2)
            {
                if (FeatureToggle.PreventHelmV2DeploymentsFeatureToggle.IsEnabled(deployment.Variables))
                {
                    throw new CommandException("Helm V2 is no longer supported. Please migrate to Helm V3.");
                }
                else
                {
                    log.Warn("This step is currently configured to use Helm V2. Support for Helm V2 will be completely removed in Octopus Server 2025.1. Please migrate to Helm V3 as soon as possible.");
                }
            }

            SetResetValuesParameter(deployment, args);
            if (helmVersion == HelmVersion.V2)
            {
                SetTillerTimeoutParameter(deployment, args);
                SetTillerNamespaceParameter(deployment, args);
            }

            SetTimeoutParameter(deployment, args);
            SetValuesParameters(deployment, args);
            var hasAdditionalArgs = SetAdditionalArguments(deployment, args);

            //Adjust args based on KOS
            if (isKOSEnabled)
            {
                AddKOSArgs(waitForJobs, hasAdditionalArgs, args);
            }

            return args;
        }

        static void AddKOSArgs(bool waitForJobs, bool hasAdditionalArgs, List<string> args)
        {
            var additionalArgs = string.Empty;
            if (hasAdditionalArgs)
            {
                //the additional args are always the last in the list
                additionalArgs = args.Last();
            }
                
            //if wait for jobs is enabled in KOS and the helm flag is not set by the user, set it
            if (!additionalArgs.Contains("--wait-for-jobs") && waitForJobs)
            {
                additionalArgs = $"{additionalArgs} --wait-for-jobs";
            }

            //if wait or atomic is not set, we need it for KOS, so just set wait
            //atomic is a superset of wait
            if (!additionalArgs.Contains("--wait") || !additionalArgs.Contains("--atomic"))
            {
                additionalArgs = $"{additionalArgs} --wait";
            }

            //update or add the additional args
            if (hasAdditionalArgs)
            {
                args[args.Count - 1] = additionalArgs;
            }
            else
            {
                args.Add(additionalArgs.Trim());
            }
        }

        static HelmVersion GetVersion(IVariables variables)
        {
            var clientVersionText = variables.Get(SpecialVariables.Helm.ClientVersion);

            if (Enum.TryParse(clientVersionText, out HelmVersion version))
                return version;

            throw new CommandException($"Unrecognized Helm version: '{clientVersionText}'");
        }

        static void SetResetValuesParameter(RunningDeployment deployment, List<string> args)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Helm.ResetValues, true))
            {
                args.Add("--reset-values");
            }
        }

        void SetValuesParameters(RunningDeployment deployment, List<string> args)
        {
            SetOrderedTemplateValues(deployment, args);

            //We leave the remaining values here as the users may not have migrated to the new structure
            foreach (var additionalValuesFile in AdditionalValuesFiles(deployment))
            {
                args.Add($"--values \"{additionalValuesFile}\"");
            }

            if (TryAddRawValuesYaml(deployment, out var rawValuesFile))
            {
                args.Add($"--values \"{rawValuesFile}\"");
            }

            if (TryGenerateVariablesFile(deployment, out var valuesFile))
            {
                args.Add($"--values \"{valuesFile}\"");
            }
        }

        void SetOrderedTemplateValues(RunningDeployment deployment, List<string> args)
        {
            var filenames = valueSourcesParser.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

            foreach (var filename in filenames)
            {
                args.Add($"--values \"{filename}\"");
            }
        }

        static bool SetAdditionalArguments(RunningDeployment deployment, List<string> args)
        {
            var additionalArguments = deployment.Variables.Get(SpecialVariables.Helm.AdditionalArguments);

            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                args.Add(additionalArguments);
                return true;
            }

            return false;
        }

        static void SetTillerNamespaceParameter(RunningDeployment deployment, List<string> args)
        {
            if (deployment.Variables.IsSet(SpecialVariables.Helm.TillerNamespace))
            {
                args.Add($"--tiller-namespace \"{deployment.Variables.Get(SpecialVariables.Helm.TillerNamespace)}\"");
            }
        }

        static void SetTimeoutParameter(RunningDeployment deployment, List<string> args)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.Timeout)) return;

            var timeout = deployment.Variables.Get(SpecialVariables.Helm.Timeout);

            if (!GoDurationParser.ValidateTimeout(timeout))
            {
                throw new CommandException($"Timeout period is not a valid duration: {timeout}");
            }

            args.Add($"--timeout \"{timeout}\"");
        }

        static void SetTillerTimeoutParameter(RunningDeployment deployment, List<string> args)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.TillerTimeout)) return;

            var tillerTimeout = deployment.Variables.Get(SpecialVariables.Helm.TillerTimeout);
            if (!int.TryParse(tillerTimeout, out _))
            {
                throw new CommandException($"Tiller timeout period is not a valid integer: {tillerTimeout}");
            }

            args.Add($"--tiller-connection-timeout \"{tillerTimeout}\"");
        }

        static string SyntaxSpecificFileName(RunningDeployment deployment, ScriptSyntax syntax)
        {
            return Path.Combine(deployment.CurrentDirectory, $"Calamari.HelmUpgrade.{SyntaxSpecificFileExtension(syntax)}");
        }

        static string SyntaxSpecificFileExtension(ScriptSyntax syntax) => syntax == ScriptSyntax.PowerShell ? "ps1" : "sh";

        string GetReleaseName(IVariables variables)
        {
            var validChars = new Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(ActionVariables.Name)}-{variables.Get(DeploymentEnvironment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            log.SetOutputVariable("ReleaseName", releaseName, variables);
            log.Info($"Using Release Name {releaseName}");
            return releaseName;
        }

        IEnumerable<string> AdditionalValuesFiles(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var packageReferenceNames = variables.GetIndexes(PackageVariables.PackageCollection);

            List<string> files = new List<string>();
            List<string> errors = new List<string>();

            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = PackageName.ExtractPackageNameFromPathedPackageId(fileSystem.RemoveInvalidFileNameChars(packageReferenceName));
                var valuesPaths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(sanitizedPackageReferenceName));
                foreach (var valuePath in valuesPaths)
                {
                    var packageId = PackageName.ExtractPackageNameFromPathedPackageId(variables.Get(PackageVariables.IndexedPackageId(packageReferenceName)));
                    var version = variables.Get(PackageVariables.IndexedPackageVersion(packageReferenceName));
                    var relativePath = Path.Combine(sanitizedPackageReferenceName, valuePath);
                    var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                    if (!currentFiles.Any() && string.IsNullOrEmpty(packageReferenceName)) // Chart archives have chart name root directory
                    {
                        log.Verbose($"Unable to find values files at path `{valuePath}`. Chart package contains root directory with chart name, so looking for values in there.");
                        var chartRelativePath = Path.Combine(packageId, relativePath);
                        currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, chartRelativePath).ToList();
                    }

                    if (!currentFiles.Any())
                    {
                        errors.Add($"Unable to find file `{valuePath}` for package {packageId} v{version}");
                    }

                    foreach (var file in currentFiles)
                    {
                        var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                        log.Info($"Including values file `{relative}` from package {packageId} v{version}");
                        files.Add(file);
                    }
                }
            }

            if (!files.Any() && errors.Any())
            {
                throw new CommandException(string.Join(Environment.NewLine, errors));
            }

            return files;
        }

        string GetChartLocation(RunningDeployment deployment)
        {
            var installDir = deployment.Variables.Get(PackageVariables.Output.InstallationDirectoryPath);

            var chartDirectoryVariable = deployment.Variables.Get(SpecialVariables.Helm.ChartDirectory);

            // Try the specific chart directory if the variable has been set
            if (!string.IsNullOrEmpty(chartDirectoryVariable))
            {
                log.Verbose($"Attempting to find chart using configured directory '{chartDirectoryVariable}'");

                var chartDirectory = Path.Combine(installDir, chartDirectoryVariable);
                if (fileSystem.DirectoryExists(chartDirectory) && fileSystem.FileExists(Path.Combine(chartDirectory, "Chart.yaml")))
                {
                    log.Verbose($"Using chart found in configured directory '{chartDirectory}'");
                    return chartDirectory;
                }

                throw new CommandException($"Chart was not found in '{chartDirectoryVariable}'");
            }

            // Try the root directory
            log.Verbose($"Attempting to find chart in root of package installation directory '{installDir}'");

            if (fileSystem.FileExists(Path.Combine(installDir, "Chart.yaml")))
            {
                log.Verbose($"Using chart found at root of package installation directory '{installDir}'");
                return installDir;
            }

            var packageId = deployment.Variables.Get(PackageVariables.IndexedPackageId(string.Empty));

            if (!string.IsNullOrEmpty(packageId))
            {
                log.Verbose($"Attempting to find chart in directory based on package id '{packageId}'");

                // Try the directory that matches the package id
                var packageIdPath = Path.Combine(installDir, packageId);
                if (fileSystem.DirectoryExists(packageIdPath) && fileSystem.FileExists(Path.Combine(packageIdPath, "Chart.yaml")))
                {
                    log.Verbose($"Using chart found in directory based on package id '{packageIdPath}'");
                    return packageIdPath;
                }
            }

            /*
             * Although conventions suggests that the directory inside the helm archive matches the package ID, this
             * can not be assumed. If the standard locations above failed to locate the Chart.yaml file, loop over
             * all subdirectories to try and find the file.
             */
            log.Verbose($"Attempting to find chart in sub-directories of package");

            foreach (var dir in fileSystem.EnumerateDirectories(installDir))
            {
                if (fileSystem.FileExists(Path.Combine(dir, "Chart.yaml")))
                {
                    log.Verbose($"Using chart found in sub-directory '{dir}'");
                    return dir;
                }
            }

            // Nothing worked
            throw new CommandException($"Unexpected error. Chart.yaml was not found in any directories inside '{installDir}'");
        }

        bool TryAddRawValuesYaml(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var yaml = deployment.Variables.Get(SpecialVariables.Helm.YamlValues);

            fileName = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, yaml);

            return fileName != null;
        }

        bool TryGenerateVariablesFile(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var variables = deployment.Variables.Get(SpecialVariables.Helm.KeyValues, "{}");
            var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(variables);

            fileName = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, values);

            return fileName != null;
        }

        void CheckHelmToolVersion(HelmCli helmCli, HelmVersion selectedVersion)
        {
            log.Verbose($"Helm version selected: {selectedVersion}");

            var (exitCode, infoOutput) = helmCli.GetExecutableVersion();
            if (exitCode != 0)
            {
                log.Warn("Unable to retrieve the Helm tool version");
                return;
            }

            var toolVersion = HelmVersionParser.ParseVersion(infoOutput);
            if (!toolVersion.HasValue)
            {
                log.Warn("Unable to parse the Helm tool version text: " + infoOutput);
            }
            else if (toolVersion.Value != selectedVersion)
            {
                log.Warn($"The Helm tool version '{toolVersion.Value}' ('{infoOutput}') doesn't match the Helm version selected '{selectedVersion}'");
            }
        }

        Dictionary<ApiResourceIdentifier, bool> GetNamespacedApiResourceDictionary()
        {
            var apiResourceLines = kubectl.ExecuteCommandAndReturnOutput("api-resources");
            apiResourceLines.Result.VerifySuccess();
            return apiResourceLines
                   .Output.InfoLogs.Skip(1)
                   .Select(line => line.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray())
                   .Where(parts => parts.Length > 3)
                   .ToDictionary(parts => new ApiResourceIdentifier(parts[2], parts[0]), parts => bool.Parse(parts[1]));
        }

        static ApiResourceIdentifier GetApiResourceIdentifier(YamlMappingNode node)
        {
            var apiVersion = node.GetChildNode<YamlScalarNode>("apiVersion").Value;
            var kind = node.GetChildNode<YamlScalarNode>("kind").Value;
            return new ApiResourceIdentifier(apiVersion, kind);
        }
    }
}