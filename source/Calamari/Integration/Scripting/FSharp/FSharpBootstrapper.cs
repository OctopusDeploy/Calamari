using System;
using System.IO;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Util;

namespace Calamari.Integration.Scripting.FSharp
{
    public static class FSharpBootstrapper
    {
        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);

        static FSharpBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(FSharpBootstrapper).Namespace + ".Bootstrap.fsx");
        }

        public static string FindExecutable()
        {
            if (!ScriptingEnvironment.IsNet45OrNewer())
                throw new CommandException("FSharp scripts require requires .NET framework 4.5");

            var myPath = typeof(FSharpEngine).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);

            var attemptOne = Path.GetFullPath(Path.Combine(parent, "FSharp", "fsi.exe"));
            if (File.Exists(attemptOne))
                return attemptOne;

            var attemptTwo = Path.GetFullPath(Path.Combine("..", "..", "packages", "FSharp.Compiler.Tools.4.0.0.1", "tools", "fsi.exe"));
            if (File.Exists(attemptTwo)) return attemptTwo;

            throw new CommandException(string.Format("fsi.exe was not found at either '{0}' or '{1}'", attemptOne, attemptTwo));
        }

        public static string FormatCommandArguments(string bootstrapFile, string scriptParameters)
        {
            var encryptionKey = Convert.ToBase64String(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("\"{0}\" {1} \"{2}\"", bootstrapFile, scriptParameters, encryptionKey);
            return commandArguments.ToString();
        }

        public static string PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));

            using (var writer = new StreamWriter(bootstrapFile, false, Encoding.UTF8))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("open Octopus");
                writer.WriteLine("Octopus.initializeProxy()");
                writer.WriteLine("#load \"" + scriptFilePath.Replace("\\", "\\\\") + "\"");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        public static string PrepareConfigurationFile(string workingDirectory, CalamariVariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".fsx");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("{{VariableDeclarations}}", WritePatternMatching(variables));

            using (var writer = new StreamWriter(configurationFile, false, Encoding.UTF8))
            {
                writer.Write(builder.ToString());
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }
            
        static string WritePatternMatching(CalamariVariableDictionary variables)
        {
            var builder = new StringBuilder();
            foreach (var variableName in variables.GetNames())
            {
                var variableValue = variables.IsSensitive(variableName)
                    ? EncryptVariable(variables.Get(variableName))
                    : EncodeValue(variables.Get(variableName));
                builder.AppendFormat("        | \"{0}\" -> \"{1}\" |> decode |> Some", EncodeValue(variableName), variableValue);
                builder.Append(Environment.NewLine);
            }
            builder.Append("        | _ -> None");

            return builder.ToString();
        }

        static string EncodeValue(string value)
        {
            if (value == null) return "null;";

            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
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