using System;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public static class ClaudePermissionsModeExtensionMethods
{
    public static string ToClaudeFlag(this ClaudePermissionMode mode)
        => mode switch
        {
            ClaudePermissionMode.DontAsk => "dontAsk",
            ClaudePermissionMode.Auto => "auto",
            _ => throw new InvalidOperationException($"Unsupported permission mode '{mode}'."),
        };
}