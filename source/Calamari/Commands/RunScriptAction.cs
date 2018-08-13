using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using System.Collections.Generic;
using System.IO;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.Scripting;

namespace Calamari.Commands
{
    [DeploymentAction("run-script", Description = "Invokes a script")]
    public class RunScriptAction : IDeploymentAction
    {
        //This can dissapear once we remove `script=` parameter
        bool scriptFromParameter = false;
        
        public void Build(IDeploymentStrategyBuilder deploymentStrategyBuilder)
        {
            deploymentStrategyBuilder.PreExecution = ((options, variables) =>
            {
                //Options.Add("package=", "Path to the package to extract that contains the script.", v => packageFile = Path.GetFullPath(v));
                options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v =>
                {
                    Log.Warn($"The `--script` parameter is deprecated.\r\n" +
                             $"Please set the `{SpecialVariables.Action.Script.ScriptBody}` and `{SpecialVariables.Action.Script.ScriptFileName}` variable to allow for variable replacement of the script file.");

                    scriptFromParameter = true;
                    variables.Set(SpecialVariables.Action.Script.ScriptFileName, v);
                    variables.Set(SpecialVariables.Action.Script.Syntax, ScriptTypeExtensions.FileNameToScriptType(v).ToString());
                });
                options.Add("scriptParameters=", $"Parameters to pass to the script.", v =>
                {
                    Log.Warn($"The `--scriptParameters` parameter is deprecated.\r\n" +
                             $"Please provide just the `{SpecialVariables.Action.Script.ScriptParameters}` variable instead.");
                    
                    variables.Set(SpecialVariables.Action.Script.ScriptParameters, v);
                });
            });
            
            deploymentStrategyBuilder
                .AddConvention<WriteVariablesScriptToFileConvention>()
                .AddStageScriptPackages()
                .AddSubsituteInFiles(_ => !scriptFromParameter, FileTargetFactory)
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddJsonVariables()
                .AddConvention<ExecuteScriptConvention>();
        }

        private IEnumerable<string> FileTargetFactory(IExecutionContext deployment)
        {
            var scriptFile = deployment.Variables.Get(SpecialVariables.Action.Script.ScriptFileName);
            yield return Path.Combine(deployment.CurrentDirectory, scriptFile);
        }
    }
}
