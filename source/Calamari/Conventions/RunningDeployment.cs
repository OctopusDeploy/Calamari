using System;
using Octostache;

namespace Calamari.Conventions
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

        public string PackageFilePath
        {
            get { return packageFilePath; }
        }

        public VariableDictionary Variables
        {
            get {  return variables; }
        }

        public void Error(Exception ex)
        {
            ex = ex.GetBaseException();
            variables.Set(SpecialVariables.LastError, ex.ToString());
            variables.Set(SpecialVariables.LastErrorMessage, ex.Message);
        }
    }
}