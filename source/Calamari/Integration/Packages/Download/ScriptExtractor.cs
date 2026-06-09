using System;
using System.IO;
using System.Reflection;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Integration.Packages.Download
{
    public static class ScriptExtractor
    {
        internal static string GetScript(ICalamariFileSystem fileSystem, string scriptName, string? outputFileNamePrefix = null)
        {
            var syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();

            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = $"{scriptName}.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = $"{scriptName}.ps1";
                    break;
                default:
                    throw new InvalidOperationException("No script wrapper exists for " + syntax);
            }

            var scriptFile = Path.Combine(".", $"{outputFileNamePrefix}{contextFile}");
            var contextScript = new AssemblyEmbeddedResources().GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"{typeof(DockerImagePackageDownloader).Namespace}.Scripts.{contextFile}");
            fileSystem.OverwriteFile(scriptFile, contextScript);
            return scriptFile;
        }
    }
}
