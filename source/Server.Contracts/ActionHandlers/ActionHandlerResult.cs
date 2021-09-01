using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ActionHandlerResult : IActionHandlerResult
    {
        public IReadOnlyDictionary<string, OutputVariable> OutputVariables { get; protected set; } = new OutputVariableCollection();
        public IReadOnlyList<ScriptOutputAction> OutputActions { get; protected set; } = new ScriptOutputAction[0];
        public IReadOnlyList<ServiceMessage> ServiceMessages { get; protected set; } = new List<ServiceMessage>();
        public ExecutionOutcome Outcome { get; protected set; }
        public bool WasSuccessful => Outcome == ExecutionOutcome.Successful;
        public string? ResultMessage { get; protected set; }
        public int ExitCode { get; protected set; }

        public void AddOutputVariable(OutputVariable variable)
        {
            ((OutputVariableCollection)OutputVariables).Add(variable);
        }

        public void AddServiceMessage(ServiceMessage message)
        {
            ((List<ServiceMessage>)ServiceMessages).Add(message);
        }

        public void SetOutcome(ExecutionOutcome outcome)
        {
            Outcome = outcome;
        }

        public static ActionHandlerResult FromSuccess()
        {
            return new()
            {
                ExitCode = 0,
                Outcome = ExecutionOutcome.Successful
            };
        }
    }
}