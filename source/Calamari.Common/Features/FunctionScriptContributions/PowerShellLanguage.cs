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
                switch (value)
                {
                    case "{":
                        sb.AppendLine($"{new string('\t', tabsCount)}{value}");
                        tabsCount++;
                        break;
                    case "}":
                        tabsCount--;
                        sb.AppendLine($"{new string('\t', tabsCount)}{value}");
                        break;
                    default:
                        sb.AppendLine($"{new string('\t', tabsCount)}{value}");
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
                TabIndentedAppendLine("$parameters = \"\"");

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
                    TabIndentedAppendLine($"$tempParameter = Convert-ToServiceMessageParameter -name \"{pair.Key}\" -value ${pair.Key}");
                    TabIndentedAppendLine("$parameters = $parameters, $tempParameter -join ' '");
                    if (!String.IsNullOrEmpty(pair.Value.DependsOn))
                    {
                        TabIndentedAppendLine("}");
                    }
                }
                TabIndentedAppendLine($"Write-Host \"##octopus[{registration.ServiceMessageName} $($parameters)]\"");

                TabIndentedAppendLine("}");

                TabIndentedAppendLine("");
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