using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Scripts;

namespace Calamari.Common.Features.FunctionScriptContributions
{
    class CodeGenFunctionsRegistry
    {
        readonly Dictionary<ScriptSyntax, ICodeGenFunctions> codeGenerators;

        public CodeGenFunctionsRegistry(IEnumerable<ICodeGenFunctions> functionCodeGenerators)
        {
            codeGenerators = functionCodeGenerators.ToDictionary(functions => functions.Syntax);
        }

        public ScriptSyntax[] SupportedScriptSyntax => codeGenerators.Keys.ToArray();
        public ICodeGenFunctions GetCodeGenerator(ScriptSyntax syntax)
        {
            return codeGenerators[syntax];
        }
    }
}