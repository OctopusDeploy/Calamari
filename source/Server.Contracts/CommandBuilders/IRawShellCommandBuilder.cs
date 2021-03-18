using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Server.Contracts.CommandBuilders
{
    /// <summary>
    /// Command that is invoked directly on the target through an open connection.
    /// This will use whatever the default shell is for that endpoint.
    /// For a Tentacle this will be it's default shell (PS for Win, Bash for other)
    /// but for SSHTargets this could be any shell, bash, busybox, insert shell here.
    /// The execution context will be whatever is default when that connection is opened
    /// </summary>
    public interface IRawShellCommandBuilder
    {
        IRawShellCommandBuilder WithScript(ScriptSyntax syntax, string body);
        IActionHandlerResult Execute(ITaskLog taskLog);
    }
}