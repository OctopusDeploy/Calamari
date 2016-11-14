using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Convention;

namespace Calamari.Features.Conventions
{
    [ConventionMetadata(CommonConventions.ExecuteScript, "Executes Scripts", true)]
    public class ScriptExecutionConvention : IInstallConvention
    {
        private readonly string scriptFile;
        private readonly string scriptParameters;
        private readonly ICalamariFileSystem fileSystem;
        private readonly CommandLineRunner commandLineRunner;
        private readonly CombinedScriptEngine scriptEngine;

        public ScriptExecutionConvention(string scriptFile, 
            string scriptParameters, 
            Shared.ILog log, 
            Shared.ICalamariFileSystem fileSystem,
             CommandLineRunner commandLineRunner,
             CombinedScriptEngine scriptEngine)
        {
            this.scriptFile = scriptFile;
            this.scriptParameters = scriptParameters;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.scriptEngine = scriptEngine;
        }

        public void Install(IVariableDictionary variables)
        {
            if (!fileSystem.FileExists(scriptFile))
                throw new CommandException("Could not find script file: " + scriptFile);

            Log.VerboseFormat("Executing '{0}'", scriptFile);

            var cvd = new CalamariVariableDictionary();
            foreach (var name in variables.GetNames())
            {
                cvd.Set(name, variables[name]);
            }

            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), cvd, commandLineRunner);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile, result.ExitCode), result.ExitCode);
            }
        }
    }
}