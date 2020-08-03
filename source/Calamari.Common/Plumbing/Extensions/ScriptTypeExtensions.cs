using System;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Scripts;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class ScriptTypeExtensions
    {
        public static string FileExtension(this ScriptSyntax scriptSyntax)
        {
            return typeof(ScriptSyntax).GetField(scriptSyntax.ToString())
                .GetCustomAttributes(typeof(FileExtensionAttribute), false)
                .Select(attr => ((FileExtensionAttribute)attr).Extension)
                .FirstOrDefault();
        }

        public static ScriptSyntax ToScriptType(this string filename)
        {
            var extension = Path.GetExtension(filename)?.TrimStart('.');
            var scriptTypeField = typeof(ScriptSyntax).GetFields()
                .SingleOrDefault(
                    field => field.GetCustomAttributes(typeof(FileExtensionAttribute), false)
                        .Any(attr => ((FileExtensionAttribute)attr).Extension == extension?.ToLower()));

            if (scriptTypeField != null)
                return (ScriptSyntax)scriptTypeField.GetValue(null);

            throw new CommandException("Unknown script-extension: " + extension);
        }
    }
}