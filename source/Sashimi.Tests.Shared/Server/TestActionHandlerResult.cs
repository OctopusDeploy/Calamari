using System.Collections.Generic;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Tests.Shared.LogParser;

namespace Sashimi.Tests.Shared.Server
{
    public class TestActionHandlerResult : IActionHandlerResult
    {
        public TestActionHandlerResult(int exitCode, IReadOnlyDictionary<string, OutputVariable> outputVariables,
            IReadOnlyList<ScriptOutputAction> outputActions, IReadOnlyList<ServiceMessage> serviceMessages,
            string resultMessage, IReadOnlyList<CollectedArtifact> artifacts, string fullLog)
        {
            ExitCode = exitCode;
            OutputVariables = outputVariables;
            OutputActions = outputActions;
            ServiceMessages = serviceMessages;
            ResultMessage = resultMessage;
            FullLog = fullLog;
            Artifacts = artifacts;
        }
        
        public string FullLog { get; }
        
        public IReadOnlyList<CollectedArtifact> Artifacts { get; }
        public IReadOnlyDictionary<string, OutputVariable> OutputVariables { get; }
        public IReadOnlyList<ScriptOutputAction> OutputActions { get; }
        public IReadOnlyList<ServiceMessage> ServiceMessages { get; }
        public ExecutionOutcome Outcome => WasSuccessful ? ExecutionOutcome.Successful : ExecutionOutcome.Unsuccessful;
        public bool WasSuccessful => ExitCode == 0;
        public string ResultMessage { get; }
        public int ExitCode { get; }
    }
}