using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Testing.LogParser;

namespace Calamari.Testing
{
    public class TestCalamariCommandResult
    {
        public TestCalamariCommandResult(int exitCode, IReadOnlyDictionary<string, TestOutputVariable> outputVariables,
            IReadOnlyList<TestScriptOutputAction> outputActions, IReadOnlyList<ServiceMessage> serviceMessages,
            string? resultMessage, IReadOnlyList<CollectedArtifact> artifacts, string fullLog, string workingPath)
        {
            ExitCode = exitCode;
            OutputVariables = outputVariables;
            OutputActions = outputActions;
            ServiceMessages = serviceMessages;
            ResultMessage = resultMessage;
            FullLog = fullLog;
            WorkingPath = workingPath;
            Artifacts = artifacts;
        }

        public string FullLog { get; }
        public string WorkingPath { get; }

        public IReadOnlyList<CollectedArtifact> Artifacts { get; }
        public IReadOnlyDictionary<string, TestOutputVariable> OutputVariables { get; }
        public IReadOnlyList<TestScriptOutputAction> OutputActions { get; }
        public IReadOnlyList<ServiceMessage> ServiceMessages { get; }
        public TestExecutionOutcome Outcome => WasSuccessful ? TestExecutionOutcome.Successful : TestExecutionOutcome.Unsuccessful;
        public bool WasSuccessful => ExitCode == 0;
        public string? ResultMessage { get; }
        public int ExitCode { get; }
    }
}