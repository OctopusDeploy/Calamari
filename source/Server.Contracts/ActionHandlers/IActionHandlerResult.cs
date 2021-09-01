using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public interface IActionHandlerResult
    {
        IReadOnlyDictionary<string, OutputVariable> OutputVariables { get; }
        IReadOnlyList<ScriptOutputAction> OutputActions { get; }
        IReadOnlyList<ServiceMessage> ServiceMessages { get; }
        ExecutionOutcome Outcome { get; }
        bool WasSuccessful { get; }
        string? ResultMessage { get; }
        int ExitCode { get; }
    }
}