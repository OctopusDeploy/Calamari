using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    class CommandLineTool
    {
        protected ILog log;
        protected ICommandLineRunner commandLineRunner;
        protected string workingDirectory;
        protected Dictionary<string, string> environmentVars;
        // TODO: this should be set per-command-invocation, rather than in this constructor. It's not yet important, but will be for later commands to ensure we don't leak generated secrets in the logs.
        protected Dictionary<string, string> redactMap;

        public CommandLineTool(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars, Dictionary<string, string> redactMap)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.workingDirectory = workingDirectory;
            this.environmentVars = environmentVars;
            this.redactMap = redactMap;
        }

        protected CommandResult ExecuteCommand(CommandLineInvocation invocation)
        {
            invocation.EnvironmentVars = environmentVars;
            invocation.WorkingDirectory = workingDirectory;
            invocation.OutputAsVerbose = false;
            invocation.OutputToLog = false;

            var captureCommandOutput = new CaptureCommandOutput();
            invocation.AdditionalInvocationOutputSink = captureCommandOutput;

            LogRedactedCommandText(invocation);

            var result = commandLineRunner.Execute(invocation);

            LogCapturedOutput(result, captureCommandOutput);

            return result;
        }

        void LogRedactedCommandText(CommandLineInvocation invocation)
        {
            var rawCommandText = invocation.ToString();
            var redactedCommandText = redactMap.Aggregate(rawCommandText, (current, pair) => current.Replace(pair.Key, pair.Value));

            log.Verbose(redactedCommandText);
        }

        void LogCapturedOutput(CommandResult result, CaptureCommandOutput captureCommandOutput)
        {
            foreach (var message in captureCommandOutput.Messages)
            {
                if (result.ExitCode == 0)
                {
                    log.Verbose(message.Text);
                    continue;
                }

                switch (message.Level)
                {
                    case Level.Info:
                        log.Verbose(message.Text);
                        break;
                    case Level.Error:
                        log.Error(message.Text);
                        break;
                }
            }
        }
    }
}