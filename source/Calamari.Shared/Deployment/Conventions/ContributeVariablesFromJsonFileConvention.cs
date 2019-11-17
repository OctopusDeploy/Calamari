using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Deployment.Conventions
{
    /// <summary>
    /// Attempts to read a file path from an environment variable and, if present,
    /// reads that file as a VariableDictionary and copies the values into the
    /// current RunningDeployment's VariableDictionary.
    /// </summary>
    public class ContributeVariablesFromJsonFileConvention : IInstallConvention
    {
        /// <summary>
        /// The name of the environment variable that specifies the file path
        /// from which to read the additional variables.
        /// </summary>
        public const string AdditionalVariablesKey = SpecialVariables.Environment.Prefix + "Octopus.AdditionalVariablesPath";

        readonly Func<string, VariableDictionary> dictionaryReader;
        
        /// <summary>
        /// Creates a default convention that attempts to read variable files
        /// using Octostache's reading functionality.
        /// </summary>
        public ContributeVariablesFromJsonFileConvention()
            : this(ReadDictionaryFromFilePath)
        {
        }
        
        /// <summary>
        /// Creates a convention that uses the provided function to read a
        /// variable dictionary from a filepath.
        /// </summary>
        public ContributeVariablesFromJsonFileConvention(Func<string, VariableDictionary> dictionaryReader)
        {
            this.dictionaryReader = dictionaryReader;
        }
        
        public void Install(RunningDeployment deployment)
        {
            if (deployment == null)
                throw new ArgumentNullException(nameof(deployment));
            
            var additionalVariablesPath = deployment.Variables.Get(AdditionalVariablesKey);

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
                      $"'{AdditionalVariablesKey}' environment variable. " +
                      $"See inner exception for details.";
            
            throw new CommandException(msg, inner);
        }
        
        /// <summary>
        /// Copies all of the variables from one variable dictionary into another.
        /// </summary>
        void CopyVariables(VariableDictionary copyFrom, CalamariVariableDictionary copyTo)
        {
            foreach (var variable in copyFrom)
            {
                copyTo.Add(variable.Key, variable.Value);
            }
        }
    }
}