using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.FSharp
{
    public static class FSharpBootstrapper
    {
        static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static FSharpBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(FSharpBootstrapper).Namespace + ".Bootstrap.fsx");
        }

        public static string FindExecutable()
        {
            if (!ScriptingEnvironment.IsNet45OrNewer())
                throw new CommandException("FSharp scripts require requires .NET framework 4.5");

            var myPath = typeof(FSharpExecutor).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);
            var executable = Path.GetFullPath(Path.Combine(parent, "FSharp", "fsi.exe"));

            if (File.Exists(executable))
                return executable;

            throw new CommandException(string.Format("fsi.exe was not found at '{0}'", executable));
        }

        public static string FormatCommandArguments(string bootstrapFile, string? scriptParameters)
        {
            var encryptionKey = Convert.ToBase64String(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("\"{0}\" {1} \"{2}\"", bootstrapFile, scriptParameters, encryptionKey);
            return commandArguments.ToString();
        }

        public static BootstrapFiles PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory, IVariables variables)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));
            var scriptModulePaths = PrepareScriptModules(variables, workingDirectory).ToArray();

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("open Octopus");
                writer.WriteLine("Octopus.initializeProxy()");
                writer.WriteLine("#load \"" + scriptFilePath.Replace("\\", "\\\\") + "\"");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return new BootstrapFiles(bootstrapFile, scriptModulePaths);
        }

        static IEnumerable<string> PrepareScriptModules(IVariables variables, string workingDirectory)
        {
            foreach (var variableName in variables.GetNames().Where(ScriptVariables.IsLibraryScriptModule))
                if (ScriptVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.FSharp)
                {
                    var libraryScriptModuleName = ScriptVariables.GetLibraryScriptModuleName(variableName);
                    var name = new string(libraryScriptModuleName.Where(char.IsLetterOrDigit).ToArray());
                    var moduleFileName = $"{name}.fsx";
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as f# module {1}. Import this module via `#load \"{1}\"`.", libraryScriptModuleName, moduleFileName, name);
                    var contents = variables.Get(variableName);
                    if (contents == null)
                        throw new InvalidOperationException($"Value for variable {variableName} could not be found.");
                    CalamariFileSystem.OverwriteFile(moduleFilePath, contents, Encoding.UTF8);
                    yield return moduleFileName;
                }
        }

        public static string PrepareConfigurationFile(string workingDirectory, IVariables variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".fsx");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("(*{{VariableDeclarations}}*)", WritePatternMatching(variables));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
            {
                writer.Write(builder.ToString());
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static string WritePatternMatching(IVariables variables)
        {
            var builder = new StringBuilder();
            foreach (var variableName in variables.GetNames())
            {
                var variableValue = variables.Get(variableName);
                if (variableValue == null)
                    builder.AppendFormat("        | \"{0}\" -> Some null", EncodeValue(variableName));
                else
                    builder.AppendFormat("        | \"{0}\" -> {1} |> Some",
                        EncodeValue(variableName),
                        EncryptVariable(variableValue));

                builder.Append(Environment.NewLine);
            }

            builder.Append("        | _ -> None");

            return builder.ToString();
        }

        static string EncodeValue(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        static string EncryptVariable(string value)
        {
            var encrypted = VariableEncryptor.Encrypt(value);
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out var iv);

            return $@"decryptString ""{Convert.ToBase64String(rawEncrypted)}"" ""{Convert.ToBase64String(iv)}""";
        }
    }
}