using System;
using System.Text;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public class FileSubstituter : IFileSubstituter
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;

        public FileSubstituter(ILog log, ICalamariFileSystem fileSystem, IVariables variables)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.variables = variables;
        }

        public void PerformSubstitution(string sourceFile)
            => PerformSubstitution(sourceFile, sourceFile);

        public void PerformSubstitution(string sourceFile, string targetFile) 
            => PerformSubstitutionAndUpdateFile(sourceFile, targetFile, variables.GetFlag(KnownVariables.ShouldFailDeploymentOnSubstitutionFails));

        protected virtual void PerformSubstitutionAndUpdateFile(string sourceFile, string targetFile, bool throwOnError)
        {
            log.Verbose($"Performing variable substitution on '{sourceFile}'");

            var source = fileSystem.ReadFile(sourceFile, out var sourceFileEncoding);
            var encoding = GetEncoding(sourceFileEncoding);

            var result = variables.Evaluate(source, out var error, false);

            if (!string.IsNullOrEmpty(error))
            {
                if (throwOnError)
                {
                    throw new Exception($"Parsing file '{sourceFile}' with Octostache returned the following error: `{error}`");
                }

                log.VerboseFormat("Parsing file '{0}' with Octostache returned the following error: `{1}`", sourceFile, error);
            }

            fileSystem.OverwriteFile(targetFile, result, encoding);
        }

        Encoding GetEncoding(Encoding fileEncoding)
        {
            var requestedEncoding = variables.Get(PackageVariables.SubstituteInFilesOutputEncoding);
            if (requestedEncoding == null)
                return fileEncoding;

            try
            {
                return Encoding.GetEncoding(requestedEncoding);
            }
            catch (ArgumentException)
            {
                return fileEncoding;
            }
        }
    }
}