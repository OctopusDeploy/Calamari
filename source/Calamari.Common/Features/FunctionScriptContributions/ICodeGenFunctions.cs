using System;
using System.Collections.Generic;
using Calamari.Common.Features.Scripts;

namespace Calamari.Common.Features.FunctionScriptContributions
{
    public interface ICodeGenFunctions
    {
        ScriptSyntax Syntax { get; }
        string Generate(IEnumerable<ScriptFunctionRegistration> registrations);
    }
}