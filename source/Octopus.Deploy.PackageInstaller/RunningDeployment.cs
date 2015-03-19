using System;
using Octostache;

namespace Octopus.Deploy.PackageInstaller
{
    public class RunningDeployment
    {
        private readonly string packageFilePath;
        private readonly VariableDictionary variables;

        public RunningDeployment(string packageFilePath, VariableDictionary variables)
        {
            this.packageFilePath = packageFilePath;
            this.variables = variables;
        }

        public void Error(Exception ex)
        {
            ex = ex.GetBaseException();
            variables.Set(SpecialVariables.LastError, ex.ToString());
            variables.Set(SpecialVariables.LastErrorMessage, ex.Message);
        }
    }
}