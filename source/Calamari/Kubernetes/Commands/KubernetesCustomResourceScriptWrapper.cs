using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes.Commands
{
    public class KubernetesCustomResourceScriptWrapper : IScriptWrapper
    {
        private readonly IVariables variables;
        private readonly ILog log;

        public KubernetesCustomResourceScriptWrapper(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }

        public int Priority => ScriptWrapperPriorities.KubernetesCustomResourcePriority;
        public IScriptWrapper NextWrapper { get; set; }

        public bool IsEnabled(ScriptSyntax syntax)
        {
            return variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName") != null;
        }

        public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var yamlGlobs = variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName")?.Split(";");

            if (yamlGlobs == null)
                return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);

            foreach (var yamlGlob in yamlGlobs)
            {

            }

            return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }
    }
}