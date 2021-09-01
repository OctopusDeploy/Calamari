using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Tests.Shared.LogParser;
using Sashimi.Server.Contracts.ActionHandlers;
using ServiceMessage = Calamari.Common.Plumbing.ServiceMessages.ServiceMessage;

namespace Sashimi.Tests.Shared.Server
{
    public class TestActionHandlerResult : IActionHandlerResult
    {
        public TestActionHandlerResult(int exitCode,
                                       IReadOnlyDictionary<string, TestOutputVariable> outputVariables,
                                       IEnumerable<TestScriptOutputAction> outputActions,
                                       IEnumerable<ServiceMessage> serviceMessages,
                                       string? resultMessage,
                                       IReadOnlyList<CollectedArtifact> artifacts,
                                       string fullLog)
        {
            ExitCode = exitCode;
            OutputVariables = outputVariables.ToDictionary(pair => pair.Key, pair => new OutputVariable(pair.Value.Name, pair.Value.Value, pair.Value.IsSensitive));
            OutputActions = outputActions.Select(action => new ScriptOutputAction(action.Name, action.Properties)).ToArray();
            ServiceMessages = serviceMessages.Select(message => new Sashimi.Server.Contracts.ActionHandlers.ServiceMessage(message.Name, new Dictionary<string, string>(message.Properties))).ToArray();
            ResultMessage = resultMessage;
            FullLog = fullLog;
            Artifacts = artifacts;
        }

        public string FullLog { get; }

        public IReadOnlyList<CollectedArtifact> Artifacts { get; }
        public IReadOnlyDictionary<string, OutputVariable> OutputVariables { get; }
        public IReadOnlyList<ScriptOutputAction> OutputActions { get; }
        public IReadOnlyList<Sashimi.Server.Contracts.ActionHandlers.ServiceMessage> ServiceMessages { get; }
        public ExecutionOutcome Outcome => WasSuccessful ? ExecutionOutcome.Successful : ExecutionOutcome.Unsuccessful;
        public bool WasSuccessful => ExitCode == 0;
        public string? ResultMessage { get; }
        public int ExitCode { get; }
    }
}