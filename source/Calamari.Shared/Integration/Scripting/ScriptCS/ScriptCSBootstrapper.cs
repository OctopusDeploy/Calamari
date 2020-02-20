using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Util;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octostache;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public static class ScriptCSBootstrapper
    {
        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

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

        public static (string bootstrapFile, string[] temporaryFiles) PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory, IVariables variables)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));
            var scriptModulePaths = PrepareScriptModules(variables, workingDirectory).ToArray();
            
            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("#load \"" + scriptFilePath.Replace("\\", "\\\\") + "\"");
                    
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return (bootstrapFile, scriptModulePaths);
        }

        static IEnumerable<string> PrepareScriptModules(IVariables variables, string workingDirectory)
        {
            foreach (var variableName in variables.GetNames().Where(SpecialVariables.IsLibraryScriptModule))
            {
                if (SpecialVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.CSharp) {
                    var libraryScriptModuleName = SpecialVariables.GetLibraryScriptModuleName(variableName);
                    var name = new string(libraryScriptModuleName.Where(char.IsLetterOrDigit).ToArray());
                    var moduleFileName = $"{name}.csx";
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as c# module {1}. Import this module via `#load {1}`.", libraryScriptModuleName, moduleFileName, name);
                    CalamariFileSystem.OverwriteFile(moduleFilePath, variables.Get(variableName), Encoding.UTF8);
                    yield return moduleFileName;
                }
            }
        }

        public static string PrepareConfigurationFile(string workingDirectory, IVariables variables)
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
            
        static string WriteVariableDictionary(IVariables variables)
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
            // We used to call System.Text.Encoding.UTF8.GetString(bytes) here, but on mono 5.10 and newer mono throws `The method or operation is not implemented.' (mono 5.8 work).
            // So, now we call System.Text.Encoding.UTF8.GetString(bytes, start, length) which has been tested and confirmed working on mono 5.8 and also 5.10 and newer.
            // See https://github.com/OctopusDeploy/Issues/issues/4404
            return $"System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(\"{Convert.ToBase64String(bytes)}\"), 0, {bytes.Length})";
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
