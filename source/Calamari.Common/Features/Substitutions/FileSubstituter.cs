using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
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

#if NETFRAMEWORK
            var fileInfo = new FileInfo(sourceFile);
            log.Verbose($"Is '{sourceFile}' on readonly mode: {fileInfo.IsReadOnly}");
            log.Verbose("File Permission Rules:");
            var aclRules = fileInfo.GetAccessControl().GetAccessRules(true, true, typeof(NTAccount));
            foreach (FileSystemAccessRule aclRule in aclRules)
            {
                log.Verbose($"User: {aclRule?.IdentityReference?.Value}");
                log.Verbose($"Rights: {aclRule?.FileSystemRights}");
                log.Verbose($"AllowOrDeny: {aclRule?.AccessControlType}");
            }
#else
            Log.Verbose("Unable to get file info due to not running under .Net Framework. Calamari will not print any file info.");
#endif
            var source = fileSystem.ReadFile(sourceFile, out var sourceFileEncoding);
            var encoding = GetEncoding(variables, sourceFileEncoding);

            var result = variables.Evaluate(source, out var error, false);

            if (!string.IsNullOrEmpty(error))
                log.VerboseFormat("Parsing file '{0}' with Octostache returned the following error: `{1}`", sourceFile, error);


            fileSystem.OverwriteFile(targetFile, result, encoding);
        }

        Encoding GetEncoding(IVariables variables, Encoding fileEncoding)
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