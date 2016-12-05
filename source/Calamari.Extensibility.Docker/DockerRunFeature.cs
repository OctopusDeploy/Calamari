using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;

namespace Calamari.Extensibility.Docker
{
    [Feature("DockerRun", "I Am A Run Script")]
    public class DockerRunFeature : DockerFeature, IFeature
    {
        public DockerRunFeature(IScriptExecution execution, ICalamariFileSystem fileSystem) : base(execution, fileSystem) { }

        public void Install(IVariableDictionary variables)
        {
            string extension;
            var script = ExtractScript("docker-run", out extension);
            var scriptPath = WriteScript(extension, script.Replace("{{Command}}", ScriptBuilder.Run(variables)));
            var result = Execution.InvokeFromFile(scriptPath, "");
        }
    }
}
