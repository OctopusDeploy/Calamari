using System;
using System.IO;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.DeploymentTools;

namespace Sashimi.Server.Contracts.CommandBuilders
{
    /// <summary>
    /// Builds a command and passes it to the IExecutor for execution
    /// There should be one implementation per bootstrapper language (ie for PowerShell, but not for C#Script)
    /// </summary>
    public interface ICalamariCommandBuilder
    {
        ICalamariCommandBuilder WithStagedPackageArgument();
        ICalamariCommandBuilder WithArgument(string name);
        ICalamariCommandBuilder WithArgument(string name, string? value);
        ICalamariCommandBuilder WithDataFile(string fileContents, string? fileName = null);
        ICalamariCommandBuilder WithDataFileNoBom(string fileContents, string? fileName = null);
        ICalamariCommandBuilder WithDataFile(byte[] fileContents, string? fileName = null);
        ICalamariCommandBuilder WithDataFile(Stream fileContents, string? fileName = null, Action<int>? progress = null);
        ICalamariCommandBuilder WithDataFileAsArgument(string argumentName, string fileContents, string? fileName = null);
        ICalamariCommandBuilder WithDataFileAsArgument(string argumentName, byte[] fileContents, string? fileName = null);
        ICalamariCommandBuilder WithTool(IDeploymentTool tool);
        ICalamariCommandBuilder WithVariable(string name, string? value, bool isSensitive = false);
        ICalamariCommandBuilder WithVariable(string name, bool value, bool isSensitive = false);
        IActionHandlerResult Execute(ITaskLog taskLog);
        ICalamariCommandBuilder WithIsolation(ExecutionIsolation executionIsolation);
        ICalamariCommandBuilder WithIsolationTimeout(TimeSpan mutexTimeout);
        string Describe();
    }

    public enum ExecutionIsolation
    {
        FullIsolation,
        NoIsolation
    }
}