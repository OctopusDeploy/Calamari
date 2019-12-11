using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Deployment.Conventions
{
    public class ContributeVariablesFromJsonFileConvention : IInstallConvention
    {
        readonly Func<string, VariableDictionary> dictionaryReader;
        
        public ContributeVariablesFromJsonFileConvention()
            : this(ReadDictionaryFromFilePath)
        {
        }
        
        public ContributeVariablesFromJsonFileConvention(Func<string, VariableDictionary> dictionaryReader)
        {
            this.dictionaryReader = dictionaryReader;
        }
        
        public void Install(RunningDeployment deployment)
        {
            if (deployment == null)
                throw new ArgumentNullException(nameof(deployment));
            
            var additionalVariablesPath = deployment.Variables.Get(SpecialVariables.AdditionalVariablesPath);

            if (string.IsNullOrEmpty(additionalVariablesPath))
                return;

            var additionalVariables = dictionaryReader(additionalVariablesPath);
            
            CopyVariables(additionalVariables, deployment.Variables);
        }

        static VariableDictionary ReadDictionaryFromFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw BuildException(filePath, "File does not exist.");
            }
            
            try
            {
                return new VariableDictionary(filePath);
            }
            catch (Exception e)
            {
                throw BuildException(filePath, "The file could not be read.", e);
            }
        }

        private static CommandException BuildException(string path, string reason, Exception inner = null)
        {
            var msg = $"Could not read additional variables from JSON file at '{path}'. " +
                      $"{reason} Make sure the file can be read or remove the " +
                      $"'{SpecialVariables.AdditionalVariablesPath}' environment variable. " +
                      $"See inner exception for details.";
            
            throw new CommandException(msg, inner);
        }
        
        void CopyVariables(VariableDictionary copyFrom, CalamariVariableDictionary copyTo)
        {
            foreach (var variable in copyFrom)
            {
                copyTo.Add(variable.Key, variable.Value);
            }
        }
    }
}