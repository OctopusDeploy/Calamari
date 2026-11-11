using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Calamari.Azure.AppServices;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    public class PowerShellWebDeploymentExecutor : IWebDeploymentExecutor
    {
        const string ScriptFileName = "ExecuteWebDeploy.ps1";

        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public PowerShellWebDeploymentExecutor(ICalamariFileSystem fileSystem,
                                               IScriptEngine scriptEngine,
                                               ICommandLineRunner commandLineRunner)
        {
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public async Task ExecuteDeployment(RunningDeployment deployment, AzureTargetSite targetSite, IVariables variables, WebDeployPublishSettings publishSettings)
        {
            var scriptFilePath = await WriteScriptToWorkingDirectory();

            AddVariablesForScript(variables, deployment, targetSite, publishSettings);

            try
            {
                //this forces the desktop (.NET Framework-based) PowerShell
                variables.Set(PowerShellVariables.Edition, null);
                
                var result = scriptEngine.Execute(new Script(scriptFilePath), variables, commandLineRunner);

                if (result.ExitCode != 0)
                {
                    throw new CommandException($"Script '{scriptFilePath}' returned non-zero exit code: {result.ExitCode}");
                }
            }
            finally
            {
                fileSystem.DeleteDirectory(Path.GetDirectoryName(scriptFilePath)!, FailureOptions.IgnoreFailure);
            }
        }

        async Task<string> WriteScriptToWorkingDirectory()
        {
            var scriptBody = await GetScriptBody();

            var tempDir = fileSystem.CreateTemporaryDirectory();
            var scriptFilePath = Path.Combine(tempDir, ScriptFileName);

            fileSystem.OverwriteFile(scriptFilePath, scriptBody);

            return scriptFilePath;
        }

        void AddVariablesForScript(IVariables variables, RunningDeployment deployment, AzureTargetSite targetSite, WebDeployPublishSettings publishSettings)
        {
            variables.Add("Octopus.Action.Azure.PowerShellExecution.SourcePath", deployment.CurrentDirectory);
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationPath", BuildPath(targetSite, variables));
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationUserName", publishSettings.PublishProfile.UserName);
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationPassword", publishSettings.PublishProfile.Password);
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationUri", publishSettings.PublishProfile.Uri.ToString());
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationDeploymentSite", publishSettings.DeploymentSite);

            variables.AddFlag("Octopus.Action.Azure.PowerShellExecution.DoNotDelete", !variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles));
            variables.AddFlag("Octopus.Action.Azure.PowerShellExecution.UseAppOffline", variables.GetFlag(SpecialVariables.Action.Azure.AppOffline));
            variables.AddFlag("Octopus.Action.Azure.PowerShellExecution.PreserveAppData", variables.GetFlag(SpecialVariables.Action.Azure.PreserveAppData));

            if (!OctopusFeatureToggles.AzureWebAppIgnorePreservePathsFeatureToggle.IsEnabled(variables))
            {
                var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');
                if (preservePaths.Count > 0)
                {
                    var pipedPaths = string.Join("|", preservePaths);
                    variables.Add("Octopus.Action.Azure.PowerShellExecution.PreservePathsPiped", pipedPaths);
                }
            }

            if (!OctopusFeatureToggles.AzureWebAppIgnoreChecksumFeatureToggle.IsEnabled(variables))
            {
                variables.AddFlag("Octopus.Action.Azure.PowerShellExecution.UseChecksum", variables.GetFlag(SpecialVariables.Action.Azure.UseChecksum));
            }
        }

        static async Task<string> GetScriptBody()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var manifestName = assembly.GetManifestResourceNames().First(name => name.EndsWith(ScriptFileName));

            await using var stream = assembly.GetManifestResourceStream(manifestName);
            if (stream is null)
            {
                throw new InvalidOperationException("Failed to load script from assembly manifest resources.");
            }

            using var streamReader = new StreamReader(stream);
            return await streamReader.ReadToEndAsync();
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