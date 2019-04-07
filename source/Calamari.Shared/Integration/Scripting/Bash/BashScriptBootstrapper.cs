using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.Processes;
using Calamari.Util;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptBootstrapper
    {
        public const string WindowsNewLine = "\r\n";

        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);

        static BashScriptBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(BashScriptBootstrapper).Namespace + ".Bootstrap.sh");
        }

        public static string FormatCommandArguments(string bootstrapFile)
        {
            var encryptionKey = ToHex(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("\"./{0}\" \"{1}\"", Path.GetFileName(bootstrapFile), encryptionKey);
            return commandArguments.ToString();
        }
        
        public static string PrepareConfigurationFile(string workingDirectory, CalamariVariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".sh");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("#### VariableDeclarations ####", string.Join("\n", GetVariableSwitchConditions(variables)));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, "\n"));
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static IEnumerable<string> GetVariableSwitchConditions(CalamariVariableDictionary variables)
        {
            return variables.GetNames().Select(variable =>
            {
                var variableValue = variables.IsSensitive(variable)
                    ? DecryptValueCommand(variables.Get(variable))
                    : string.Format("decode_servicemessagevalue \"{0}\"", EncodeValue(variables.Get(variable)));

                return string.Format("    \"{1}\"){0}   {2}   ;;{0}", "\n", EncodeValue(variable), variableValue);
            });
        }

        static string DecryptValueCommand(string value)
        {
            var encrypted = VariableEncryptor.Encrypt(value ?? "");
            byte[] iv;
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out iv);
            
            return string.Format("decrypt_variable \"{0}\" \"{1}\"", Convert.ToBase64String(rawEncrypted), ToHex(iv));
        }

        static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        static string EncodeValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public static string FindBashExecutable()
        {
            //TODO: Get this working on Non mono (windows path on cygwin)
            //return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/bin/bash" : @"C:\cygwin64\bin\bash.exe";
            return "bash";
        }

        static void EnsureValidUnixFile(string scriptFilePath)
        {
            var text = File.ReadAllText(scriptFilePath);
            text = text.Replace(WindowsNewLine, "\n");
            File.WriteAllText(scriptFilePath, text);
        }

        public static string PrepareBootstrapFile(Script script, string configurationFile, string workingDirectory)
        {            
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(script.File));

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.NewLine = "\n";
                writer.WriteLine("#!/bin/bash");
                writer.WriteLine("source \"./" + Path.GetFileName(configurationFile) + "\" ");
                writer.WriteLine("source \"./" + Path.GetFileName(script.File) + "\" " + script.Parameters);
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            EnsureValidUnixFile(script.File);
            return bootstrapFile;
        }
    }
}
