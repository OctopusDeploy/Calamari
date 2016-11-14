using System.Collections.Generic;
using Calamari.Integration.Substitutions;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;
using Octostache;

namespace Calamari.Features.Conventions
{
    [ConventionMetadata(CommonConventions.VariableReplacement, "Replaces Variables", true)]
    public class VariableReplacementConvention : IInstallConvention
    {
        private readonly string fileName;
        private readonly ICalamariFileSystem fileSystem;

        public VariableReplacementConvention(string fileName, ICalamariFileSystem fileSystem)
        {
            this.fileName = fileName;
            this.fileSystem = fileSystem;
        }


        public void Install(IVariableDictionary variables)
        {
            if (!fileSystem.FileExists(fileName))
            {
                Log.WarnFormat("The file '{0}' could not be found for variable substitution.", fileName);
                return;
            }

            Log.Info("Performing variable substitution on '{0}'", fileName);

            var substituter = new FileSubstituter((Integration.FileSystem.ICalamariFileSystem)fileSystem);
            substituter.PerformSubstitution(fileName, (VariableDictionary)variables);
        }
    }
}