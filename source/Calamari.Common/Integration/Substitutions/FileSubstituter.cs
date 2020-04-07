using System;
using System.Text;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octostache;

namespace Calamari.Integration.Substitutions
{
    public class FileSubstituter : IFileSubstituter
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public FileSubstituter(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public void PerformSubstitution(string sourceFile, IVariables variables)
        {
            PerformSubstitution(sourceFile, variables, sourceFile);
        }

        public void PerformSubstitution(string sourceFile, IVariables variables, string targetFile)
        {
            log.Verbose($"Performing variable substitution on '{sourceFile}'");

            var source = fileSystem.ReadFile(sourceFile, out var sourceFileEncoding);
            var encoding = GetEncoding(variables, sourceFileEncoding);

            var result = variables.Evaluate(source, out var error, haltOnError: false);

            if (!string.IsNullOrEmpty(error))
                log.VerboseFormat("Parsing file '{0}' with Octostache returned the following error: `{1}`", sourceFile, error);

            fileSystem.OverwriteFile(targetFile, result, encoding);
        }

        private Encoding GetEncoding(IVariables variables, Encoding fileEncoding)
        {
            var requestedEncoding = variables.Get(PackageVariables.SubstituteInFilesOutputEncoding);
            if (requestedEncoding == null)
            {
                return fileEncoding;
            }

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
