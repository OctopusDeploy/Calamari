using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public interface IScriptActionOverride
    {
        ScriptActionOverrideResult ShouldOverride(DeploymentTargetType deploymentTargetType, IActionHandlerContext context);
    }

    public abstract class ScriptActionOverrideResult
    {
        public static ScriptActionOverrideResult RedirectToHandler<THandle>() where THandle : IActionHandler
        {
            return new RedirectToScriptHandlerResult(typeof(THandle));
        }

        public static ScriptActionOverrideResult RunDefaultAction()
        {
            return new RunDefaultScriptActionResult();
        }
    }

    public class RunDefaultScriptActionResult : ScriptActionOverrideResult
    {
        internal RunDefaultScriptActionResult()
        {
        }
    }

    public class RedirectToScriptHandlerResult : ScriptActionOverrideResult
    {
        internal RedirectToScriptHandlerResult(Type handler)
        {
            Handler = handler;
        }

        public Type Handler { get; }
    }
}