using Calamari.Integration.Processes;
using Octopus.CoreUtilities.Extensions;
using System.Collections.Specialized;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Aws.Integration
{
    /// <summary>
    /// A custom script engine that first builds the AWS authentication and region environment
    /// variables before running the script with the standard powershell script engine.
    /// </summary>
    public class AwsPowerShellScriptEngine : IScriptEngine
    {
        public ScriptType[] GetSupportedTypes()
        {
            return new[] { ScriptType.Powershell };
        }

        public CommandResult Execute(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null)
        {
            /*
             * Use AwsEnvironmentGeneration to generate the environment variables that are required
             * by any custom AWS script, and then pass these through to the standard PowerShell
             * script engine.
             */
            return new AwsEnvironmentGeneration(variables)
                .Map(envDetails => new PowerShellScriptEngine().Execute(
                    script, 
                    variables, 
                    commandLineRunner, 
                    envDetails.EnvironmentVars));
        }
    }
}