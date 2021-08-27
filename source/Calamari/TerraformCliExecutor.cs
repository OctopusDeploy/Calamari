using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Helpers;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace Calamari.Terraform
{
    class TerraformCliExecutor : IDisposable
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly RunningDeployment deployment;
        readonly IVariables variables;
        readonly Dictionary<string, string> environmentVariables;
        readonly string templateDirectory;
        readonly string logPath;
        Dictionary<string, string> defaultEnvironmentVariables;
        readonly TemporaryDirectory disposableDirectory = TemporaryDirectory.Create();
        bool haveLoggedUntestedVersionInfoMessage = false;

        readonly VersionRange supportedVersionRange = new VersionRange(NuGetVersion.Parse("0.11.15"), true, NuGetVersion.Parse("1.1"), false);

        public TerraformCliExecutor(
            ILog log,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            RunningDeployment deployment,
            Dictionary<string, string> environmentVariables
        )
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.deployment = deployment;
            variables = deployment.Variables;
            this.environmentVariables = environmentVariables;
            logPath = Path.Combine(deployment.CurrentDirectory, "terraform.log");

            /*
             * Terraform has an issue where it will not clean up temporary files created while downloading
             * providers: https://github.com/hashicorp/terraform/issues/28477
             *
             * By overriding the temporary directory and cleaning it up when Calamari is done,
             * we can work around the the issue.
             *
             * https://golang.org/pkg/os/#TempDir
             * On Unix systems, it returns $TMPDIR if non-empty, else /tmp. On Windows,
             * it uses GetTempPath, returning the first non-empty value from %TMP%,
             * %TEMP%, %USERPROFILE%, or the Windows directory. On Plan 9, it returns /tmp.
             */
            this.environmentVariables["TMP"] = disposableDirectory.DirectoryPath;
            this.environmentVariables["TEMP"] = disposableDirectory.DirectoryPath;
            this.environmentVariables["TMPDIR"] = disposableDirectory.DirectoryPath;

            templateDirectory = variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, deployment.CurrentDirectory);

            if (!string.IsNullOrEmpty(templateDirectory))
            {
                var templateDirectoryTemp = Path.Combine(deployment.CurrentDirectory, templateDirectory);

                if (!Directory.Exists(templateDirectoryTemp))
                    throw new Exception($"Directory {templateDirectory} does not exist.");

                templateDirectory = templateDirectoryTemp;
            }

            InitializeTerraformEnvironmentVariables();

            Version = GetVersion();

            InitializePlugins();

            InitializeWorkspace();
        }

        public Version Version { get; }

        public string ActionParams => variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams);

        /// <summary>
        /// Create a list of -var-file arguments from the newline separated list of variable files
        /// </summary>
        public string TerraformVariableFiles
        {
            get
            {
                var varFilesAsString = deployment.Variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles);

                if (varFilesAsString == null) return null;

                var varFiles = Regex.Split(varFilesAsString, "\r?\n")
                                    .Select(var => $"-var-file=\"{var}\"")
                                    .ToList();

                return string.Join(" ", varFiles);
            }
        }

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            return ExecuteCommandAndVerifySuccess(arguments, out var result, true);
        }

        public CommandResult ExecuteCommand(out string result, params string[] arguments)
        {
            return ExecuteCommand(out result, true, arguments);
        }

        public CommandResult ExecuteCommand(out string result, bool outputToCalamariConsole, params string[] arguments)
        {
            return ExecuteCommandInternal(arguments, out result, outputToCalamariConsole);
        }

        public void Dispose()
        {
            var attachLogFile = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AttachLogFile);
            if (attachLogFile)
            {
                var crashLogPath = Path.Combine(deployment.CurrentDirectory, "crash.log");

                if (fileSystem.FileExists(logPath))
                    log.NewOctopusArtifact(fileSystem.GetFullPath(logPath), fileSystem.GetFileName(logPath), fileSystem.GetFileSize(logPath));

                //When terraform crashes, the information would be contained in the crash.log file. We should attach this since
                //we don't want to blow that information away in case it provides something relevant https://www.terraform.io/docs/internals/debugging.html#interpreting-a-crash-log
                if (fileSystem.FileExists(crashLogPath))
                    log.NewOctopusArtifact(fileSystem.GetFullPath(crashLogPath), fileSystem.GetFileName(crashLogPath), fileSystem.GetFileSize(crashLogPath));
            }
            disposableDirectory.Dispose();
        }

        public void VerifySuccess(CommandResult commandResult, Predicate<CommandResult> isSuccess)
        {
            LogUntestedVersionMessageIfNeeded(commandResult, isSuccess);

            if (isSuccess == null || !isSuccess(commandResult))
                commandResult.VerifySuccess();
        }

        public void VerifySuccess(CommandResult commandResult)
        {
            VerifySuccess(commandResult, r => r.ExitCode == 0);
        }

        void LogUntestedVersionMessageIfNeeded(CommandResult commandResult, Predicate<CommandResult> isSuccess)
        {
            if (this.Version != null && !supportedVersionRange.Satisfies(new NuGetVersion(Version)))
            {
                var messageCode = "Terraform-Configuration-UntestedTerraformCLIVersion";
                var message = $"{log.FormatLink($"https://g.octopushq.com/Terraform#{messageCode.ToLower()}", messageCode)}: Terraform steps are tested against versions {(supportedVersionRange.IsMinInclusive ? "" : ">")}{supportedVersionRange.MinVersion.ToNormalizedString()} to {(supportedVersionRange.IsMaxInclusive ? "" : "<")}{supportedVersionRange.MaxVersion.ToNormalizedString()} of the Terraform CLI. Version {Version} of Terraform CLI has not been tested, however Terraform commands may work successfully with this version. Click the error code link for more information.";
                if (isSuccess == null || !isSuccess(commandResult))
                {
                    log.Warn(message);
                }
                else if (!haveLoggedUntestedVersionInfoMessage) // Only want to log an info message once, not on every command
                {
                    log.Info(message);
                    haveLoggedUntestedVersionInfoMessage = true;
                }
            }
        }

        CommandResult ExecuteCommandAndVerifySuccess(string[] arguments, out string result, bool outputToCalamariConsole)
        {
            var commandResult = ExecuteCommandInternal(arguments, out result, outputToCalamariConsole);
            VerifySuccess(commandResult);
            return commandResult;
        }

        CommandResult ExecuteCommandInternal(string[] arguments, out string result, bool outputToCalamariConsole)
        {
            var environmentVar = defaultEnvironmentVariables;
            if (environmentVariables != null)
                environmentVar.AddRange(environmentVariables);

            var terraformExecutable = variables.Get(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable) ?? $"terraform{(CalamariEnvironment.IsRunningOnWindows ? ".exe" : string.Empty)}";
            var captureOutput = new CaptureInvocationOutputSink();
            var commandLineInvocation = new CommandLineInvocation(terraformExecutable, arguments)
            {
                WorkingDirectory = templateDirectory,
                EnvironmentVars = environmentVar,
                OutputToLog = outputToCalamariConsole,
                AdditionalInvocationOutputSink = captureOutput
            };

            log.Info(commandLineInvocation.ToString());

            var commandResult = commandLineRunner.Execute(commandLineInvocation);

            result = string.Join("\n", captureOutput.Infos);

            return commandResult;
        }

        void InitializePlugins()
        {
            var initParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams);
            var allowPluginDownloads = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true);
            string initCommand = $"init -no-color";

            if (Version?.IsLessThan("0.15.0") == true)
                initCommand += $" -get-plugins={allowPluginDownloads.ToString().ToLower()}";

            initCommand += $" {initParams}";

            ExecuteCommandAndVerifySuccess(new[] { initCommand }, out _, true);
        }

        class TerraformVersionCommandOutput
        {
            [JsonProperty("terraform_version")]
            public string Version { get; set; }
        }

        Version GetVersion()
        {
            ExecuteCommandAndVerifySuccess(new[] { "version --json" }, out string consoleOutput, true);

            Version parsedVersion = null;
            bool hasParsingFailed = false;
            var versionJsonOutput = JsonConvert.DeserializeObject<TerraformVersionCommandOutput>(consoleOutput, new JsonSerializerSettings
            {
                // this prevents NewtonsoftJson from throwing an exception
                Error = (sender, args) =>
                        {
                            hasParsingFailed = true;
                            args.ErrorContext.Handled = true;
                        }
            });

            if (hasParsingFailed || !Version.TryParse(versionJsonOutput.Version, out parsedVersion))
            {
                // fallback to regex match for older versions
                var versionString = Regex.Match(consoleOutput, @"Terraform v([0-9\.]*)");
                if (versionString.Success
                    && versionString.Groups.Count > 1
                    && !Version.TryParse(versionString.Groups[1].Value, out parsedVersion))
                {
                    log.Warn($"Could not parse Terraform CLI version. This might indicate you are using a version that is not supported or that an unexpected output was received from Terraform CLI.");
                }
            }

            return parsedVersion;
        }

        void InitializeWorkspace()
        {
            var workspace = variables.Get(TerraformSpecialVariables.Action.Terraform.Workspace);

            if (!string.IsNullOrWhiteSpace(workspace))
            {
                ExecuteCommandAndVerifySuccess(new[] { "workspace list" }, out var results, true);

                foreach (var line in results.Split('\n'))
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(workspace))
                    {
                        ExecuteCommandAndVerifySuccess(new[] { $"workspace select \"{workspace}\"" }, out _, true);
                        return;
                    }
                }

                ExecuteCommandAndVerifySuccess(new[] { $"workspace new \"{workspace}\"" }, out _, true);
            }
        }

        void InitializeTerraformEnvironmentVariables()
        {
            defaultEnvironmentVariables = ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables().ToDictionary(e => e.Key, e => e.Value);

            defaultEnvironmentVariables.Add("TF_IN_AUTOMATION", "1");
            defaultEnvironmentVariables.Add("TF_LOG", "TRACE");
            defaultEnvironmentVariables.Add("TF_LOG_PATH", logPath);
            defaultEnvironmentVariables.Add("TF_INPUT", "0");

            var customPluginDir = deployment.Variables.Get(TerraformSpecialVariables.Action.Terraform.PluginsDirectory);
            var pluginsPath = Path.Combine(deployment.CurrentDirectory, "terraformplugins");

            fileSystem.EnsureDirectoryExists(pluginsPath);

            if (!string.IsNullOrEmpty(customPluginDir))
                fileSystem.CopyDirectory(customPluginDir, pluginsPath);

            defaultEnvironmentVariables.Add("TF_PLUGIN_CACHE_DIR", pluginsPath);
        }

        class CaptureInvocationOutputSink : ICommandInvocationOutputSink
        {
            public List<string> Infos { get; } = new List<string>();

            public void WriteInfo(string line)
            {
                Infos.Add(line);
            }

            public void WriteError(string line)
            {
            }
        }
    }
}