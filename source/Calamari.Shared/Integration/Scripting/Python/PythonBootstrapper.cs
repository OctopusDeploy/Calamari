using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.Bash;
using Calamari.Util;

namespace Calamari.Integration.Scripting.Python
{
    public class PythonBootstrapper
    {
        public const string WindowsNewLine = "\r\n";

        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static PythonBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(PythonBootstrapper).Namespace + ".Bootstrap.py");
        }

        public static string FormatCommandArguments(string bootstrapFile)
        {
            var encryptionKey = ToHex(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.Append($"\"{bootstrapFile}\" \"{encryptionKey}\"");
            return commandArguments.ToString();
        }
        
        public static string PrepareConfigurationFile(string workingDirectory, CalamariVariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".py");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("#### VariableDeclarations ####", string.Join(Environment.NewLine, GetVariables(variables)));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, Environment.NewLine));
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static IEnumerable<string> GetVariables(CalamariVariableDictionary variables)
        {
            return variables.GetNames().Select(variable =>
            {
                var variableValue = variables.IsSensitive(variable)
                    ? DecryptValueCommand(variables.Get(variable))
                    : $"decode(\"{EncodeValue(variables.Get(variable))}\")";

                return $"\"{EncodeValue(variable)}\" : {variableValue}";
            });
        }

        static string DecryptValueCommand(string value)
        {
            var encrypted = VariableEncryptor.Encrypt(value ?? "");
            byte[] iv;
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out iv);
            
            return $"decrypt_variable(\"{Convert.ToBase64String(rawEncrypted)}\" \"{ToHex(iv)}\")";
        }

        static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        static string EncodeValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public static string FindPythonExecutable()
        {
            var myPath = typeof(PythonBootstrapper).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);
            var executable = Path.GetFullPath(Path.Combine(parent, "Python", "python.exe"));

            if (File.Exists(executable))
                return executable;

            throw new CommandException($"python.exe.exe was not found at '{executable}'");
        }

        static void EnsureValidUnixFile(string scriptFilePath)
        {
            var text = File.ReadAllText(scriptFilePath);
            text = text.Replace(WindowsNewLine, Environment.NewLine);
            File.WriteAllText(scriptFilePath, text);
        }

        public static string PrepareBootstrapFile(Script script, string workingDirectory, CalamariVariableDictionary variables)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(script.File));

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("{{TargetScriptFile}}", script.File.EscapeSingleQuotedString())
                .Replace("{{ScriptParameters}}", script.Parameters)
                .Replace("{{VariableDeclarations}}", $"octopusvariables = {{ {string.Join(",", GetVariables(variables))} }}");

            CalamariFileSystem.OverwriteFile(bootstrapFile, builder.ToString(), new UTF8Encoding(true));

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            EnsureValidUnixFile(script.File);
            return bootstrapFile;
        }
    
    }
}