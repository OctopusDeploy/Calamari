using System;
using System.IO;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Util;
using System.Reflection;
using Calamari.Shared;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public static class ScriptCSBootstrapper
    {
        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);

        static ScriptCSBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(ScriptCSBootstrapper).Namespace + ".Bootstrap.csx");
        }

        public static string FindExecutable()
        {
            if (!ScriptingEnvironment.IsNet45OrNewer())
                throw new CommandException("ScriptCS scripts require the Roslyn CTP, which requires .NET framework 4.5");

            var myPath = typeof(ScriptCSScriptEngine).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);
            var executable = Path.GetFullPath(Path.Combine(parent, "ScriptCS", "scriptcs.exe"));

            if (File.Exists(executable))
                return executable;

            throw new CommandException(string.Format("ScriptCS.exe was not found at '{0}'", executable));
        }

        public static string FormatCommandArguments(string bootstrapFile, string scriptParameters)
        {
            scriptParameters = RetrieveParameterValues(scriptParameters);
            var encryptionKey = Convert.ToBase64String(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("-script \"{0}\" -- {1} \"{2}\"", bootstrapFile, scriptParameters, encryptionKey);
            return commandArguments.ToString();
        }

        private static string RetrieveParameterValues(string scriptParameters)
        {
            if (scriptParameters == null) return null;
            return scriptParameters.Trim()
                                   .TrimStart('-')
                                   .Trim();
        }

        public static string PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
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
            builder.Replace("/*{{VariableDeclarations}}*/", WriteVariableDictionary(variables));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
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

            var encrypted = VariableEncryptor.Encrypt(value);
            byte[] iv;
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out iv);

            return string.Format("DecryptString(\"{0}\", \"{1}\")", Convert.ToBase64String(rawEncrypted), Convert.ToBase64String(iv));
        }
    }
}
