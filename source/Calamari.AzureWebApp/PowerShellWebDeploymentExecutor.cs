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
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    public class PowerShellWebDeploymentExecutor :IWebDeploymentExecutor
    {
        const string ScriptFileName = "ExecuteWebDeploy.ps1";
        
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public PowerShellWebDeploymentExecutor(IScriptEngine scriptEngine,
                                               ICommandLineRunner commandLineRunner)
        {
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }
        
        public async Task ExecuteDeployment(RunningDeployment deployment, AzureTargetSite targetSite, IVariables variables, WebDeployPublishSettings publishSettings)
        {
            var scriptBody = await GetScriptBody();
            var script = new Script(scriptBody);

            AddVariablesForScript(variables, deployment, targetSite, publishSettings);
            
            var result = scriptEngine.Execute(script, variables, commandLineRunner);
        }

        void AddVariablesForScript(IVariables variables, RunningDeployment deployment, AzureTargetSite targetSite, WebDeployPublishSettings publishSettings)
        {
            variables.Add("Octopus.Action.Azure.PowerShellExecution.SourcePath", deployment.CurrentDirectory);
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationPath", BuildPath(targetSite, variables));
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationUserName", publishSettings.PublishProfile.UserName);
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationPassword", publishSettings.PublishProfile.Password);
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationUri", publishSettings.PublishProfile.Uri.ToString());
            variables.Add("Octopus.Action.Azure.PowerShellExecution.DestinationDeploymentSite", publishSettings.DeploymentSite);
            
            var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');
            if (preservePaths.Count > 0)
            {
                var pipedPaths = string.Join("|", preservePaths.Select(s => $"\"{s}\""));
                variables.Add("Octopus.Action.Azure.PowerShellExecution.PreservePathsPiped", pipedPaths);
            }

            // if these feature toggles are set, clear out these variable values
            if (OctopusFeatureToggles.AzureWebAppIgnorePreservePathsFeatureToggle.IsEnabled(variables))
            {
                variables.Set(SpecialVariables.Action.Azure.PreservePaths, null);
            }

            if (OctopusFeatureToggles.AzureWebAppIgnoreChecksumFeatureToggle.IsEnabled(variables))
            {
                variables.Set(SpecialVariables.Action.Azure.UseChecksum, null);
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