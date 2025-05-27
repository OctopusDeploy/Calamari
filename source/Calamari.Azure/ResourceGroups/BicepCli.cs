using System;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Azure.ResourceGroups
{
    public class BicepCli
    {
        public const string ArmTemplateFileName = "ARMTemplate.json";
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly string workingDirectory;
        string azCliLocation = null!;

        public BicepCli(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.workingDirectory = workingDirectory;

            SetAz();
        }

        public string BuildArmTemplate(string bicepFilePath)
        {
            var invocation = new CommandLineInvocation(azCliLocation,
                                                       "bicep",
                                                       "build",
                                                       "--file",
                                                       bicepFilePath,
                                                       "--outfile",
                                                       ArmTemplateFileName)
            {
                WorkingDirectory = workingDirectory
            };

            ExecuteCommandLineInvocationAndLogOutput(invocation);
            
            return Path.Combine(workingDirectory, ArmTemplateFileName);
        }

        void SetAz()
        {
            var result = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteRawCommandAndReturnOutput("where", "az.cmd")
                : ExecuteRawCommandAndReturnOutput("which", "az");

            var infoMessages = result.Output.Messages.Where(m => m.Level == Level.Verbose).Select(m => m.Text).ToArray();
            var foundExecutable = infoMessages.FirstOrDefault();
            if (string.IsNullOrEmpty(foundExecutable))
                throw new CommandException("Could not find az. Make sure az is on the PATH.");

            azCliLocation = foundExecutable.Trim();
        }

        CommandResultWithOutput ExecuteRawCommandAndReturnOutput(string exe, params string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                WorkingDirectory = workingDirectory,
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput
            };

            var result = commandLineRunner.Execute(invocation);

            return new CommandResultWithOutput(result, captureCommandOutput);
        }

        CommandResult ExecuteCommandLineInvocationAndLogOutput(CommandLineInvocation invocation)
        {
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
                    case Level.Verbose:
                        log.Verbose(message.Text);
                        break;
                    case Level.Error:
                        log.Error(message.Text);
                        break;
                }
            }
        }
    }

    class CommandResultWithOutput
    {
        public CommandResultWithOutput(CommandResult result, CaptureCommandOutput output)
        {
            Result = result;
            Output = output;
        }

        public CommandResult Result { get; }

        public CaptureCommandOutput Output { get; set; }
    }
}