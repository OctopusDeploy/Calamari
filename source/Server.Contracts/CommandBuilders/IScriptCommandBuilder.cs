using System;
using System.IO;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Server.Contracts.CommandBuilders
{
    /// <summary>
    /// Command that is invoked using default shell for that endpoint.
    /// For a Tentacle this will be it's default shell (PS for Win, Bash for other)
    /// and for SSHTargets this will default to Bash.
    /// This script is executed from within the context of a temporary working directory
    /// </summary>
    public interface IScriptCommandBuilder
    {
        IScriptCommandBuilder WithScript(ScriptSyntax syntax, string body);
        IScriptCommandBuilder WithDataFile(string fileContents, string? fileName = null);
        IScriptCommandBuilder WithDataFileNoBom(string fileContents, string? fileName = null);
        IScriptCommandBuilder WithDataFile(byte[] fileContents, string? fileName = null);
        IScriptCommandBuilder WithDataFile(Stream fileContents, string? fileName = null);
        IScriptCommandBuilder WithIsolation(ExecutionIsolation executionIsolation);
        IScriptCommandBuilder WithIsolationTimeout(TimeSpan mutexTimeout);
        IActionHandlerResult Execute(ITaskLog taskLog);
    }
}