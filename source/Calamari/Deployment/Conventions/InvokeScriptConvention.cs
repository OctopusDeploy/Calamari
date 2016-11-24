using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Commands.Support;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{

    public class ScriptExecution : IScriptExecution
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly IScriptEngine scriptEngine;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly IVariableDictionary variables;

        public ScriptExecution(ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, IVariableDictionary variables)
        {
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
        }

        public void Invoke(string scriptFile, string scriptParameters)
        {
            if (!fileSystem.FileExists(scriptFile))
                throw new CommandException("Could not find script file: " + scriptFile);

            Log.VerboseFormat("Executing '{0}'", scriptFile);
            
            
            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), variables, commandLineRunner);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile, result.ExitCode), result.ExitCode);
            }
        }
    }


    public class InvokeScriptConvention : IInstallConvention
    {
        private readonly string scriptFile;
        private readonly string scriptParameters;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IScriptEngine scriptEngine;
        private readonly ICommandLineRunner commandLineRunner;

        public InvokeScriptConvention(string scriptFile, string scriptParameters, ICalamariFileSystem fileSystem, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.scriptFile = scriptFile;
            this.scriptParameters = scriptParameters;
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!fileSystem.FileExists(scriptFile))
                throw new CommandException("Could not find script file: " + scriptFile);

            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), deployment.Variables, commandLineRunner);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile, result.ExitCode), result.ExitCode);
            }
        }
    }
}
