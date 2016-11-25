using Calamari.Extensibility;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Calamari.Shared;

namespace Calamari.Features
{
    public class FileSubstitution : IFileSubstitution
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly IFileSubstituter substituter;
        private readonly IVariableDictionary variableDictionary;
        private readonly ILog log;

        public FileSubstitution(ICalamariFileSystem fileSystem, IFileSubstituter substituter, IVariableDictionary variableDictionary, ILog log)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            this.variableDictionary = variableDictionary;
            this.log = log;
        }

        public void PerformSubstitution(string sourceFile)
        {
            if (!variableDictionary.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled))
                return;

            if (!fileSystem.FileExists(sourceFile))
            {
                log.WarnFormat($"The file '{sourceFile}' could not be found for variable substitution.");
                return;
            }
            log.Info($"Performing variable substitution on '{sourceFile}'");
            substituter.PerformSubstitution(sourceFile, variableDictionary);
        }
    }
}