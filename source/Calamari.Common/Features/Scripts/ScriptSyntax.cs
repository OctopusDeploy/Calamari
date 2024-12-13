using System;

namespace Calamari.Common.Features.Scripts
{
    public enum ScriptSyntax
    {
        [FileExtension("ps1")]
        PowerShell,

        [FileExtension("csx")]
        CSharp,

        [FileExtension("sh")]
        Bash,

        [FileExtension("py")]
        Python
    }
}