using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public class ManifestReportScriptWrapper : IScriptWrapper
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IManifestRetriever manifestRetriever;
        readonly IManifestReporter manifestReporter;
    
        public ManifestReportScriptWrapper(
            IVariables variables, 
            ICalamariFileSystem fileSystem,
            IManifestRetriever manifestRetriever,
            IManifestReporter manifestReporter)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.manifestRetriever = manifestRetriever;
            this.manifestReporter = manifestReporter;
        }
        public int Priority => ScriptWrapperPriorities.KubernetesManifestReportPriority;
        public IScriptWrapper NextWrapper { get; set; }
        public bool IsEnabled(ScriptSyntax syntax) => variables.IsKubernetesScript();
    
        public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner, Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var manifests = manifestRetriever.GetManifests(workingDirectory);

            foreach (var manifest in manifests)
            {
                manifestReporter.ReportManifestApplied(manifest);
            }
            
            return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }
    }
}