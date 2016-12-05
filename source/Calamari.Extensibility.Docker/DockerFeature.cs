using System;
using System.IO;
using System.Reflection;
using System.Text;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;

namespace Calamari.Extensibility.Docker
{
    public abstract class DockerFeature
    {
        protected readonly IScriptExecution Execution;
        protected readonly ICalamariFileSystem FileSystem;

        protected DockerFeature(IScriptExecution execution, ICalamariFileSystem fileSystem)
        {
            this.Execution = execution;
            this.FileSystem = fileSystem;
        }

        protected string WriteScript(string extension, string script)
        {
            string scriptPath = "";
            using (var scriptStream = FileSystem.CreateTemporaryFile(extension, out scriptPath))
            using (var writer = new StreamWriter(scriptStream, Encoding.UTF8))
            {
                writer.Write(script);
            }
            return scriptPath;
        }

        protected string ExtractScript(string scriptName, out string extension)
        {
            using (var resourceStream = GetRunnableScript(scriptName, out extension))
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        Stream GetRunnableScript(string scriptName, out string extension)
        {
            foreach (var ext in Execution.SupportedExtensions)
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