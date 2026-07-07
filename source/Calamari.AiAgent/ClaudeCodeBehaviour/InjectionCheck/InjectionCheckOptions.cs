using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.InjectionCheck;

// Every value here is hardcoded for the spike but resolved through IVariables so it can be
// promoted to step configuration later by setting the matching Octopus.Action.Claude.* variable.
public class InjectionCheckOptions
{
    const string DefaultModel = "claude-haiku-4-5";
    const int DefaultMaxTokens = 1024;
    const int DefaultMaxInputCharacters = 200_000;

    public bool Enabled { get; init; }
    public string Model { get; init; } = DefaultModel;
    public int MaxTokens { get; init; } = DefaultMaxTokens;
    public int MaxInputCharacters { get; init; } = DefaultMaxInputCharacters;
    public InjectionCheckAction OnDetection { get; init; }
    public bool FailOpenOnError { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public static InjectionCheckOptions Resolve(IVariables variables)
    {
        var model = variables.Get(SpecialVariables.Action.Claude.InjectionCheckModel);

        return new InjectionCheckOptions
        {
            Enabled = variables.GetFlag(SpecialVariables.Action.Claude.InjectionCheckEnabled, true),
            Model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            MaxTokens = variables.GetInt32(SpecialVariables.Action.Claude.InjectionCheckMaxTokens) ?? DefaultMaxTokens,
            MaxInputCharacters = variables.GetInt32(SpecialVariables.Action.Claude.InjectionCheckMaxInputCharacters) ?? DefaultMaxInputCharacters,
            OnDetection = ResolveOnDetection(variables),
            FailOpenOnError = variables.GetFlag(SpecialVariables.Action.Claude.InjectionCheckFailOpenOnError, false),
        };
    }

    static InjectionCheckAction ResolveOnDetection(IVariables variables)
    {
        var raw = variables.Get(SpecialVariables.Action.Claude.InjectionCheckOnDetection);
        if (string.IsNullOrWhiteSpace(raw))
            return InjectionCheckAction.Block;

        if (Enum.TryParse<InjectionCheckAction>(raw, ignoreCase: true, out var action))
            return action;

        throw new CommandException($"Unknown value '{raw}' for '{SpecialVariables.Action.Claude.InjectionCheckOnDetection}'. Expected one of: {string.Join(", ", Enum.GetNames(typeof(InjectionCheckAction)))}.");
    }
}
