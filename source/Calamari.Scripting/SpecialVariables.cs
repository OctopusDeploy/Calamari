using System;
using Calamari.Common.Features.Scripts;

namespace Calamari.Scripting
{
    static class SpecialVariables
    {
        public static class Packages
        {
            public static string ExtractedPath(string key)
            {
                return $"Octopus.Action.Package[{key}].ExtractedPath";
            }

            public static string PackageFileName(string key)
            {
                return $"Octopus.Action.Package[{key}].PackageFileName";
            }

            public static string PackageFilePath(string key)
            {
                return $"Octopus.Action.Package[{key}].PackageFilePath";
            }
        }

        public static class Action
        {
            public const string SkipRemainingConventions = "Octopus.Action.SkipRemainingConventions";
            public const string FailScriptOnErrorOutput = "Octopus.Action.FailScriptOnErrorOutput";

            public static class Script
            {
                public static readonly string ScriptParameters = "Octopus.Action.Script.ScriptParameters";
                public static readonly string ScriptSource = "Octopus.Action.Script.ScriptSource";
                public static readonly string ExitCode = "Octopus.Action.Script.ExitCode";

                public static string ScriptBodyBySyntax(ScriptSyntax syntax)
                {
                    return $"Octopus.Action.Script.ScriptBody[{syntax.ToString()}]";
                }
            }
        }
    }
}