using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public interface IActionHandlerWithAccount : IActionHandler
    {
        string[] StepBasedVariableNameForAccountIds { get; }
    }
}