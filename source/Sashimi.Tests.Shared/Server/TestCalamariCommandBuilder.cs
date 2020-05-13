using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari;
using Calamari.Integration.FileSystem;
using Calamari.Terraform;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.DeploymentTools;

namespace Sashimi.Tests.Shared.Server
{
    public class TestCalamariCommandBuilder<TCalamariProgram> : ICalamariCommandBuilder where TCalamariProgram : CalamariFlavourProgram
    {
        TestVariableDictionary variables = new TestVariableDictionary();

        public CalamariFlavour? CalamariFlavour { get; set; }
        public string? CalamariCommand { get; set; }
        public List<(string? filename, string contents, bool hasBom)> Files = new List<(string?, string, bool)>();
        public List<(string name, string? value)> Arguments = new List<(string, string?)>();
        public List<string> Extensions = new List<string>();

        public IList<IDeploymentTool> Tools { get;} = new List<IDeploymentTool>();

        public ICalamariCommandBuilder WithStagedPackageArgument()
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithArgument(string name)
        {
            Arguments.Add((name, null));
            return this;
        }

        public ICalamariCommandBuilder WithArgument(string name, string value)
        {
            Arguments.Add((name, value));
            return this;
        }

        public ICalamariCommandBuilder WithExtension(string extension)
        {
            Extensions.Add(extension);
            return this;
        }

        public ICalamariCommandBuilder WithDataFile(string fileContents, string? fileName = null)
        {
            Files.Add((fileName, fileContents, true));
            return this;
        }

        public ICalamariCommandBuilder WithDataFileNoBom(string fileContents, string? fileName = null)
        {
            Files.Add((fileName, fileContents, false));
            return this;
        }

        public ICalamariCommandBuilder WithDataFile(byte[] fileContents, string? fileName = null)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithDataFile(Stream fileContents, string? fileName = null, Action<int>? progress = null)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithDataFileAsArgument(string argumentName, string fileContents, string? fileName = null)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithDataFileAsArgument(string argumentName, byte[] fileContents, string? fileName = null)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithTool(IDeploymentTool tool)
        {
            Tools.Add(tool);
            return this;
        }


        public ICalamariCommandBuilder WithVariable(string name, string value, bool isSensitive = false)
        { 
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithVariable(string name, bool value, bool isSensitive = false)
            => throw new NotImplementedException();

        public IActionHandlerResult Execute()
        {
            var inMemoryLog = new InMemoryLog();
            using (var working = TemporaryDirectory.Create())
            {
                Directory.CreateDirectory(working.DirectoryPath);

                //TODO: set this as the working directory
                var originalWorkingDirectory = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = working.DirectoryPath;

                    var args = new List<string> {CalamariCommand!};

                    args.AddRange(
                        Arguments
                            .Select(a => $"--{a.name}{(a.value == null ? "" : $"={a.value}")}")
                    );
                    args.AddRange(Extensions.Select(e => $"--extension={e}"));

                    var varPath = Path.Combine(working.DirectoryPath, "variables.json");
                    variables.Save(varPath);
                    args.Add($"--variables={varPath}");

                    //TODO: Deal with sensitive variables
                    // variableArgs += $" -sensitiveVariables=\"{sshBashPaths.BuildPath(sshBashPaths.WorkingDirectory, "variables.secret")}\" -sensitiveVariablesPassword=$1";
                    foreach (var (filename, contents, _) in Files)
                    {
                        File.WriteAllText(Path.Combine(working.DirectoryPath, filename!), contents);
                    }

                    var constructor = typeof(TCalamariProgram).GetConstructor(
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] {typeof(ILog)}, new ParameterModifier[0]);
                    var instance = (TCalamariProgram) constructor?.Invoke(new object?[]
                    {
                        inMemoryLog
                    })!;

                    var methodInfo = typeof(CalamariFlavourProgram).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (methodInfo == null)
                    {
                        throw new Exception("CalamariFlavourProgram.Run method was not found");
                    }
                    var exitCode = (int)methodInfo.Invoke(instance, new object?[] {args.ToArray()})!;
                        
                    return new TestActionHandlerResult(exitCode, inMemoryLog.StandardOut);
                }
                finally
                {
                    Environment.CurrentDirectory = originalWorkingDirectory;
                }
            }
        }

        public ICalamariCommandBuilder WithIsolation(ExecutionIsolation executionIsolation)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithIsolationTimeout(TimeSpan mutexTimeout)
            => throw new NotImplementedException();

        public string Describe()
            => throw new NotImplementedException();

        public void SetVariables(TestVariableDictionary variables)
        {
            this.variables = variables;
        }
    }
    
    public class TestActionHandlerResult : IActionHandlerResult
    {
        public TestActionHandlerResult(int exitCode, List<string> stdOut)
        {
            ExitCode = exitCode;
            Output = stdOut;
            OutputVariables = new Dictionary<string, OutputVariable>();
            OutputActions = new List<ScriptOutputAction>();
            ServiceMessages = new List<ServiceMessage>();
        }
        
        public List<string> Output { get; } = new List<string>();

        public IReadOnlyDictionary<string, OutputVariable> OutputVariables { get; set; }
        public IReadOnlyList<ScriptOutputAction> OutputActions { get; set; }
        public IReadOnlyList<ServiceMessage> ServiceMessages { get; set; }
        public ExecutionOutcome Outcome => WasSuccessful ? ExecutionOutcome.Successful : ExecutionOutcome.Unsuccessful;
        public bool WasSuccessful => ExitCode == 0;
        public string? ResultMessage { get; set; }
        public int ExitCode { get; set; }
    }
    
    public class InMemoryLog : AbstractLog
    {
        public List<Message> Messages { get; } = new List<Message>();
        public List<string> StandardOut { get; } = new List<string>();
        public List<string> StandardError  { get; }= new List<string>();

        protected override void StdOut(string message)
        {
            Console.WriteLine(message); // Write to console for the test output
            StandardOut.Add(message);
        }

        protected override void StdErr(string message)
        {
            Console.Error.WriteLine(message);
            StandardError.Add(message);
        }

        public override void Verbose(string message)
        {
            Messages.Add(new Message(Level.Verbose, message));
            base.Verbose(message);
        }

        public override void VerboseFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Verbose, messageFormat, args));
            base.VerboseFormat(messageFormat, args);
        }

        public override void Info(string message)
        {
            Messages.Add(new Message(Level.Info, message));
            base.Info(message);
        }

        public override void InfoFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Info, messageFormat, args));
            base.InfoFormat(messageFormat, args);
        }

        public override void Warn(string message)
        {
            Messages.Add(new Message(Level.Warn, message));
            base.Warn(message);
        }

        public override void WarnFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Warn, messageFormat, args));
            base.WarnFormat(messageFormat, args);
        }

        public override void Error(string message)
        {
            Messages.Add(new Message(Level.Error, message));
            base.Error(message);
        }

        public override void ErrorFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Error, messageFormat, args));
            base.ErrorFormat(messageFormat, args);
        }


        public class Message
        {
            public Level Level { get; }
            public string MessageFormat { get; }
            public object[] Args { get; }
            public string FormattedMessage { get; }

            public Message(Level level, string message, params object[] args)
            {
                Level = level;
                MessageFormat = message;
                Args = args;
                FormattedMessage = args == null ? message : string.Format(message, args);
            }
        }

        public enum Level
        {
            Verbose,
            Info,
            Warn,
            Error
        }
    }
}