using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;

namespace Calamari.Extensibility.Docker
{
    [Feature("DockerRun", "I Am A Run Script")]
    public class DockerRunFeature : IFeature
    {
        private readonly IScriptExecution execution;
        private readonly ICalamariFileSystem fileSystem;

        public DockerRunFeature(IScriptExecution execution, ICalamariFileSystem fileSystem)
        {
            this.execution = execution;
            this.fileSystem = fileSystem;
        }

        public void Install(IVariableDictionary variables)
        {
            string extension;
            var script = ExtractScript(out extension);
            var scriptPath = WriteScript(extension, script.Replace("{{Command}}", ScriptBuilder.Run(variables)));

            var result = execution.InvokeFromFile(scriptPath, "");
        }

        private string WriteScript(string extension, string script)
        {
            string scriptPath = "";
            using (var scriptStream = fileSystem.CreateTemporaryFile(extension, out scriptPath))
            using (var writer = new StreamWriter(scriptStream, Encoding.UTF8))
            {
                writer.Write(script);
            }
            return scriptPath;
        }

        private string ExtractScript(out string extension)
        {
            using (var resourceStream = GetRunnableScript("docker-run", out extension))
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        Stream GetRunnableScript(string scriptName, out string extension)
        {
            foreach (var ext in execution.SupportedExtensions)
            {
                var assembly = this.GetType().GetTypeInfo().Assembly;
                var resourceStream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Scripts.{scriptName}.{ext}");
                if (resourceStream != null)
                {
                    extension = ext;
                    return resourceStream;
                }
            }
            throw new Exception($"Unable to find runnable script named `{scriptName}`");
        }

    }
}
