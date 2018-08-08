namespace Calamari.Shared.Scripting
{
    
    public enum ScriptSyntax
    {
        [FileExtension("ps1")]
        PowerShell,

        [FileExtension("csx")]
        CSharp,

        [FileExtension("sh")]
        Bash,

        [FileExtension("fsx")]
        FSharp
    }
}