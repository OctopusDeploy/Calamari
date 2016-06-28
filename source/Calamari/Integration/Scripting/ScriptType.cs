using System.Linq;
using Calamari.Commands.Support;

namespace Calamari.Integration.Scripting
{
    public enum ScriptType
    {
        [FileExtension("ps1")]
        Powershell,

        [FileExtension("csx")]
        ScriptCS,

        [FileExtension("sh")]
        Bash,

        [FileExtension("fsx")]
        FSharp
    }

    public static class ScriptTypeExtensions
    {
        public static string FileExtension(this ScriptType scriptType)
        {
            return typeof (ScriptType).GetField(scriptType.ToString())
                    .GetCustomAttributes(typeof (FileExtensionAttribute), false)
                    .Select(attr => ((FileExtensionAttribute) attr).Extension)
                    .FirstOrDefault();
        }

        public static ScriptType ToScriptType(this string extension)
        {
            var scriptTypeField = typeof (ScriptType).GetFields()
                .SingleOrDefault(
                    field => field.GetCustomAttributes(typeof (FileExtensionAttribute), false)
                            .Any(attr => ((FileExtensionAttribute) attr).Extension == extension.ToLower()));

            if (scriptTypeField != null)
                return (ScriptType)scriptTypeField.GetValue(null);

            throw new CommandException("Unknown script-extension: " + extension);
        }
    }
}