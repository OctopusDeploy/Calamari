using System;
using System.Collections.Generic;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Plugin;


namespace Calamari.Commands
{
    [Command("helloworld", Description = "Example command that can be run without any variables files (i.e. can be debugged directly)")]
    class HelloWorldCommand : Command
    {
        private string name = "world";
        private readonly CombinedScriptEngine scriptEngine;
        private readonly IEnumerable<IScriptEnvironment> environmentPlugins;

        public HelloWorldCommand(
            CombinedScriptEngine scriptEngine,
            IEnumerable<IScriptEnvironment> environmentPlugins)
        {
            Options.Add("name=", "The name to greet", x => name = x);
            this.scriptEngine = scriptEngine;
            this.environmentPlugins = environmentPlugins;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var variables = new CalamariVariableDictionary();
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            var filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var semaphore = SemaphoreFactory.Get();

            var retValue = InvokeScript(variables);
            return retValue;
        }

        private int InvokeScript(CalamariVariableDictionary variables)
        {
            var runner = new CommandLineRunner(
                new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var result = scriptEngine.Execute(new Script("C:\\Users\\matth\\Desktop\\awsscript.ps1", ""), variables, runner, environmentPlugins.MergeDictionaries());

            return result.ExitCode;
        }
    }
}
