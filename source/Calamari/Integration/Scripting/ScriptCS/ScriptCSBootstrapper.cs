using System;
using System.IO;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Util;
using Octostache;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public static class ScriptCSBootstrapper
    {
        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = Guid.NewGuid().ToString();

        static ScriptCSBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(ScriptCSBootstrapper).Namespace + ".Bootstrap.csx");
        }

        public static string FindScriptCSExecutable()
        {
            if (!IsNet45OrNewer())
                throw new CommandException("ScriptCS scripts require the Roslyn CTP, which requires .NET framework 4.5");

            var myPath = typeof(ScriptCSScriptEngine).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);

            var attemptOne = Path.GetFullPath(Path.Combine(parent, "ScriptCS", "scriptcs.exe"));
            if (File.Exists(attemptOne))
                return attemptOne;

            var attemptTwo = Path.GetFullPath(Path.Combine("..", "..", "packages", "Octopus.Dependencies.ScriptCS.3.0.1", "runtime", "scriptcs.exe"));
            if (File.Exists(attemptTwo))
                return attemptTwo;

            throw new CommandException(string.Format("ScriptCS.exe was not found at either '{0}' or '{1}'", attemptOne, attemptTwo));
        }

        public static string FormatCommandArguments(string bootstrapFile)
        {
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("-script \"{0}\" -- \"{1}\"", bootstrapFile, SensitiveVariablePassword);
            return commandArguments.ToString();
        }

        static bool IsNet45OrNewer()
        {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }

        public static string PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));

            using (var writer = new StreamWriter(bootstrapFile, false, Encoding.UTF8))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("#load \"" + scriptFilePath.Replace("\\", "\\\\") + "\"");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        public static string PrepareConfigurationFile(string workingDirectory, CalamariVariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".csx");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("{{VariableDeclarations}}", WriteVariableDictionary(variables));

            using (var writer = new StreamWriter(configurationFile, false, Encoding.UTF8))
            {
                writer.Write(builder.ToString());
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }
            
        static string WriteVariableDictionary(CalamariVariableDictionary variables)
        {
            var builder = new StringBuilder();
            foreach (var variable in variables.GetNames())
            {
                var variableValue = variables.IsSensitive(variable)
                    ? EncryptVariable(variables.Get(variable))
                    : EncodeValue(variables.Get(variable));
                builder.Append("\t\t\tthis[").Append(EncodeValue(variable)).Append("] = ").Append(variableValue).AppendLine(";");
            }
            return builder.ToString();
        }

        static string EncodeValue(string value)
        {
            if (value == null)
                return "null;";

            var bytes = Encoding.UTF8.GetBytes(value);
            return string.Format("System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(\"{0}\"))", Convert.ToBase64String(bytes));
        }

        static string EncryptVariable(string value)
        {
            if (value == null)
                return "null;";

            var variableEncryptor = new ScriptVariableEncryptor(SensitiveVariablePassword);
            return string.Format("DecryptString(\"{0}\")", variableEncryptor.Encrypt(value));
        }
    }
}