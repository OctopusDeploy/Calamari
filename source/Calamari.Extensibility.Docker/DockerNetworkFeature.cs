using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;

namespace Calamari.Extensibility.Docker
{
    [Feature("DockerNetwork", "I Am A Run Script", Module = typeof(DockerModule))]
    public class DockerNetworkFeature : DockerFeature, IFeature
    {
        private readonly RandomStringGenerator generator;

        public DockerNetworkFeature(IScriptExecution execution, ICalamariFileSystem fileSystem, RandomStringGenerator generator) :
            base(execution, fileSystem)
        {
            this.generator = generator;
        }

        public void Install(IVariableDictionary variables)
        {
            string extension;
            var script = ExtractScript("docker-network", out extension);
            var scriptPath = WriteScript(extension, script.Replace("{{Command}}", ScriptBuilder.Network(generator, variables)));
            var result = Execution.InvokeFromFile(scriptPath, "");
        }
    }

    public class DockerModule :IModule
    {
        public void Register(ICalamariContainer container)
        {
            container.RegisterInstance(new RandomStringGenerator());
        }
    }
}
