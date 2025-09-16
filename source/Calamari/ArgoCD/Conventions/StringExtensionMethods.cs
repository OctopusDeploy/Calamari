#nullable enable
using System;

namespace Calamari.ArgoCD.Conventions;

public static class StringExtensionMethods
{
    public static string? DetectLineEnding(this string input)
    {
        return input.Contains("\r\n") 
            ? "\r\n" 
            : input.Contains("\n") 
                ? "\n" 
                : input.Contains("\r") 
                    ? "\r" 
                    : null;
    }
}