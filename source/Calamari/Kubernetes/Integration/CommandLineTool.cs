using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    public class CommandLineTool
    {
        protected readonly ILog log;
        protected readonly string workingDirectory;
        protected readonly Dictionary<string, string> environmentVars;

        readonly ICommandLineRunner commandLineRunner;

        protected CommandLineTool(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.workingDirectory = workingDirectory;
            this.environmentVars = environmentVars;
        }

        public string ExecutableLocation { get; protected set; }

        protected bool TryExecuteCommandAndLogOutput(string exe, params string[] arguments)
        {
            var result = ExecuteCommandAndLogOutput(new CommandLineInvocation(exe, arguments));
            return result.ExitCode == 0;
        }

        protected CommandResult ExecuteCommandAndLogOutput(CommandLineInvocation invocation)
        {
            invocation.EnvironmentVars = environmentVars;
            invocation.WorkingDirectory = workingDirectory;
            invocation.OutputAsVerbose = false;
            invocation.OutputToLog = false;

            var captureCommandOutput = new CaptureCommandOutput();
            invocation.AdditionalInvocationOutputSink = captureCommandOutput;

            LogCommandText(invocation);

            var result = commandLineRunner.Execute(invocation);

            LogCapturedOutput(result, captureCommandOutput);

            return result;
        }

        void LogCommandText(CommandLineInvocation invocation)
        {
            log.Verbose(invocation.ToString());
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

        protected CommandResultWithOutput ExecuteCommandAndReturnOutput(string exe, params string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                EnvironmentVars = environmentVars,
                WorkingDirectory = workingDirectory,
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput
            };

            var result = commandLineRunner.Execute(invocation);

            return new CommandResultWithOutput(result, captureCommandOutput);
        }
    }

    public class CommandResultWithOutput
    {
        public CommandResultWithOutput(CommandResult result, ICommandOutput output)
        {
            Result = result;
            Output = output;
        }

        public CommandResult Result { get; }

        public ICommandOutput Output { get; set; }
    }
}
