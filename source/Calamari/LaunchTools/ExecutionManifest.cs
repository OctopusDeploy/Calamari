using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.LaunchTools
{
    public class Instruction
    {
        public LaunchTools Launcher { get; set; }
        public JToken LauncherInstructions { get; set; }
        public string LauncherInstructionsRaw => LauncherInstructions.ToString();
    }

    public interface IExecutionManifest
    {
        Instruction[] Create(string path);
    }

    public class ExecutionManifest : IExecutionManifest
    {
        readonly ICalamariFileSystem fileSystem;

        public ExecutionManifest(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public Instruction[] Create(string path)
        {
            var contents = RetrieveExecutionManifest(path);

            var instructions = JsonConvert.DeserializeObject<Instruction[]>(contents, JsonSerialization.GetDefaultSerializerSettings());

            if (instructions.Length == 0) throw new ArgumentException("The execution manifest must have at least one instruction", nameof(instructions));

            return instructions;
        }

        string RetrieveExecutionManifest(string path)
        {
            if (!fileSystem.FileExists(path))
                throw new CommandException($"Could not find execution manifest: {path}");

            return fileSystem.ReadFile(path);
        }
    }
}