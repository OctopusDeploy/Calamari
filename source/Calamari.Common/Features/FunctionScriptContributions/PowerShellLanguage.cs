using System;
using System.Collections.Generic;
using System.Text;
using Calamari.Common.Features.Scripts;

namespace Calamari.Common.Features.FunctionScriptContributions
{
    class PowerShellLanguage : ICodeGenFunctions
    {
        public ScriptSyntax Syntax { get; } = ScriptSyntax.PowerShell;

        public string Generate(IEnumerable<ScriptFunctionRegistration> registrations)
        {
            var sb = new StringBuilder();
            var tabsCount = 0;

            void TabIndentedAppendLine(string value)
            {
                var tabs = new string('\t', tabsCount);
                sb.AppendLine($"{tabs}{value}");
                switch (value)
                {
                    case "{":
                        tabsCount++;
                        break;
                    case "}":
                        tabsCount--;
                        break;
                }
            }

            foreach (var registration in registrations)
            {
                sb.Append($"function New-{registration.Name}(");

                var count = 0;
                foreach (var pair in registration.Parameters)
                {
                    if (count > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append($"[{ConvertType(pair.Value.Type)}] ${pair.Key}");
                    count++;
                }

                TabIndentedAppendLine(")");
                TabIndentedAppendLine("{");

                foreach (var pair in registration.Parameters)
                {
                    if (!String.IsNullOrEmpty(pair.Value.DependsOn))
                    {
                        if (registration.Parameters.TryGetValue(pair.Value.DependsOn, out var dependsOnProperty))
                        {
                            switch (dependsOnProperty.Type)
                            {
                                case ParameterType.String:
                                    TabIndentedAppendLine($"if (![string]::IsNullOrEmpty(${pair.Value.DependsOn}))");
                                    break;
                                case ParameterType.Bool:
                                    TabIndentedAppendLine($"if (${pair.Value.DependsOn} -eq $true)");
                                    break;
                                case ParameterType.Int:
                                    TabIndentedAppendLine($"if (${pair.Value.DependsOn} -gt 0)");
                                    break;
                            }
                        }

                        TabIndentedAppendLine("{");
                    }
                    TabIndentedAppendLine($"${pair.Key} = Convert-ToServiceMessageParameter -name \"{pair.Key}\" -value ${pair.Key}");
                    TabIndentedAppendLine($"$parameters = $parameters, ${pair.Key} -join ' '");
                    if (!String.IsNullOrEmpty(pair.Value.DependsOn))
                    {
                        TabIndentedAppendLine("}");
                    }
                }
                TabIndentedAppendLine($"Write-Host \"##octopus[{registration.ServiceMessageName} $($parameters)]\"");

                TabIndentedAppendLine("}");
            }

            sb.AppendLine("Write-Verbose \"Invoking target script $OctopusFunctionAppenderTargetScript with $OctopusFunctionAppenderTargetScriptParameters parameters.\"");
            sb.AppendLine("Invoke-Expression \". `\"$OctopusFunctionAppenderTargetScript`\" $OctopusFunctionAppenderTargetScriptParameters\"");

            return sb.ToString();
        }

        string ConvertType(ParameterType type)
        {
            return type switch
                   {
                       ParameterType.String => "string",
                       ParameterType.Bool => "switch",
                       ParameterType.Int => "int",
                       _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                   };
        }
    }
}