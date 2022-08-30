using System;

namespace Calamari.Common.Features.StructuredVariables
{
    public static class StructuredConfigMessages
    {
        public static readonly string NoStructuresFound = "No structures have been found that match variable names, so no structured variable replacements have been applied.";

        public static string StructureFound(string name)
        {
            return $"Structure found matching the variable '{name}'. Replacing its content with the variable value.";
        }
    }
}