using System.IO;
using System.Linq;
using Calamari.Commands.Support;

namespace Calamari.Integration.Scripting
{
    public enum ScriptSyntax
    {
        [FileExtension("ps1")]
        Powershell,

        [FileExtension("csx")]
        CSharp,

        [FileExtension("sh")]
        Bash,

        [FileExtension("fsx")]
        FSharp
    }

    public static class ScriptTypeExtensions
    {
        public static string FileExtension(this ScriptSyntax scriptSyntax)
        {
            return typeof (ScriptSyntax).GetField(scriptSyntax.ToString())
                    .GetCustomAttributes(typeof (FileExtensionAttribute), false)
                    .Select(attr => ((FileExtensionAttribute) attr).Extension)
                    .FirstOrDefault();
        }

        public static ScriptSyntax FileNameToScriptType(string filename)
        {
            var extension = Path.GetExtension(filename)?.TrimStart('.');
            if (extension.TryToScriptType(out var syntax))
            {
                return syntax;
            }

            throw new CommandException("Unknown script-extension: " + extension);
        }

        public static bool TryToScriptType(this string extension, out ScriptSyntax syntax)
        {
            var scriptTypeField = typeof(ScriptSyntax).GetFields()
                .SingleOrDefault(
                    field => field.GetCustomAttributes(typeof(FileExtensionAttribute), false)
                        .Any(attr => ((FileExtensionAttribute)attr).Extension == extension.ToLower()));

            if (scriptTypeField != null)
            {
                syntax = (ScriptSyntax) scriptTypeField.GetValue(null);
                return true;
            }

            syntax = 0;
            return false;
        }
    }
}