using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.GoogleCloudScripting
{
    public class GoogleCloudContextScriptWrapper : IScriptWrapper
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IVariables variables;
        readonly ScriptSyntax[] supportedScriptSyntax = {ScriptSyntax.PowerShell, ScriptSyntax.Bash};

        public GoogleCloudContextScriptWrapper(IVariables variables, ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
        }

        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        public bool IsEnabled(ScriptSyntax syntax) => supportedScriptSyntax.Contains(syntax);

        public IScriptWrapper? NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File)!;
            variables.Set("OctopusGoogleCloudTargetScript", script.File);
            variables.Set("OctopusGoogleCloudTargetScriptParameters", script.Parameters);
            using var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory, scriptSyntax));

            var jsonKey = variables.Get(SpecialVariables.Action.GoogleCloud.JsonKey, String.Empty);
            var bytes = Convert.FromBase64String(jsonKey);
            using (var keyFile = new TemporaryFile(Path.Combine(workingDirectory, Path.GetRandomFileName())))
            {
                File.WriteAllBytes(keyFile.FilePath, bytes);
                variables.Set("OctopusGoogleCloudKeyFile", keyFile.FilePath);
                return NextWrapper!.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax,
                    commandLineRunner, environmentVars);
            }
        }

        string CreateContextScriptFile(string workingDirectory, ScriptSyntax syntax)
        {
            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = "GoogleCloudContext.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = "GoogleCloudContext.ps1";
                    break;
                default:
                    throw new InvalidOperationException($"No Azure context wrapper exists for {syntax}");
            }

            var contextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Namespace}.Scripts.{contextFile}");
            fileSystem.OverwriteFile(contextScriptFile, contextScript);
            return contextScriptFile;
        }
    }
}