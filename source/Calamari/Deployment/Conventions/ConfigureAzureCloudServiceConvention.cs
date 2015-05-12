using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.Azure;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Octostache;

namespace Calamari.Deployment.Conventions
{
    public class ConfigureAzureCloudServiceConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ISubscriptionCloudCredentialsFactory credentialsFactory;
        readonly IAzureCloudServiceConfigurationRetriever configurationRetriever;

        public const string ConfigurationFileVariable = "OctopusAzureConfigurationFile";
        public const string ConfigurationFileNameVariable = "OctopusAzureConfigurationFileName";

        public ConfigureAzureCloudServiceConvention(ICalamariFileSystem fileSystem, ISubscriptionCloudCredentialsFactory subscriptionCloudCredentialsFactory, IAzureCloudServiceConfigurationRetriever configurationRetriever)
        {
            this.fileSystem = fileSystem;
            this.credentialsFactory = subscriptionCloudCredentialsFactory;
            this.configurationRetriever = configurationRetriever;
        }

        public void Install(RunningDeployment deployment)
        {
            var configurationFile = ChooseWhichConfigurationFileToUse(deployment);
            deployment.Variables.SetOutputVariable(ConfigurationFileVariable, configurationFile);

            var configuration = XDocument.Parse(fileSystem.ReadFile(configurationFile));
            UpdateConfigurationWithCurrentInstanceCount(configuration, configurationFile, deployment.Variables);
            UpdateConfigurationSettings(configuration, deployment.Variables);
            SaveConfigurationFile(configuration, configurationFile);
        }

        string ChooseWhichConfigurationFileToUse(RunningDeployment deployment)
        {
            var configurationFilePath = deployment.Variables.Get(ConfigurationFileVariable);

            if (!string.IsNullOrWhiteSpace(configurationFilePath) && !fileSystem.FileExists(configurationFilePath))
            {
                throw new CommandException("The specified Azure service configuraton file does not exist: " + configurationFilePath);
            }

            if (string.IsNullOrWhiteSpace(configurationFilePath))
            {
                configurationFilePath = GetFirstExistingFile(deployment,
                    deployment.Variables.Get(ConfigurationFileNameVariable),
                    "ServiceConfiguration." + deployment.Variables.Get(SpecialVariables.Environment.Name) + ".cscfg",
                    "ServiceConfiguration.Cloud.cscfg");
            }

            return configurationFilePath;
        }

        string GetFirstExistingFile(RunningDeployment deployment, params string[] fileNames)
        {
            foreach (var name in fileNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var path = Path.Combine(deployment.CurrentDirectory, name);
                if (fileSystem.FileExists(path))
                {
                    Log.Verbose("Found Azure service configuration file: " + path);
                    return path;
                }
                
                Log.Verbose("Azure service configuration file not found: " + path);
            }

            throw new CommandException("Could not find an Azure service configuration file in the package.");
        }

        void SaveConfigurationFile(XDocument document, string configurationFilePath)
        {
            fileSystem.OverwriteFile(configurationFilePath, document.ToString());
        }

        static void UpdateConfigurationSettings(XContainer configurationFile, VariableDictionary variables)
        {
            Log.Verbose("Updating configuration settings...");
            var foundSettings = false;

            WithConfigurationSettings(configurationFile, (roleName, settingName, settingValueAttribute) =>
            {
                var setting = variables.Get(roleName + "/" + settingName) ??
                            variables.Get(roleName + "\\" + settingName) ??
                            variables.Get(settingName);
                
                if (setting != null)
                {
                    foundSettings = true;
                    Log.Info("Updating setting for role {0}: {1} = {2}", roleName, settingName, setting);
                    settingValueAttribute.Value = setting;
                }
            });

            if (!foundSettings)
            {
                Log.Info("No settings that match provided variables were found.");
            }
        }

        void UpdateConfigurationWithCurrentInstanceCount(XContainer localConfigurationFile, string configurationFileName, VariableDictionary variables)
        {
            if (!variables.GetFlag(SpecialVariables.Machine.Azure.UseCurrentInstanceCount))
                return;

            var serviceName = variables.Get(SpecialVariables.Machine.Azure.CloudServiceName);
            var slot = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), variables.Get(SpecialVariables.Machine.Azure.Slot));

            var remoteConfigurationFile = configurationRetriever.GetConfiguration(
                credentialsFactory.GetCredentials(variables.Get(SpecialVariables.Machine.Azure.SubscriptionId),
                    variables.Get(SpecialVariables.Machine.Azure.CertificateThumbprint),
                    variables.Get(SpecialVariables.Machine.Azure.CertificateBytes)),
                serviceName,
                slot);

            if (remoteConfigurationFile == null)
            {
                Log.Info("There is no current deployment of service '{0}' in slot '{1}', so existing instance counts will not be imported.", serviceName, slot);
                return;
            }

            var rolesByCount = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Log.Verbose("Local instance counts (from " + Path.GetFileName(configurationFileName) + "): ");
            WithInstanceCounts(localConfigurationFile, (roleName, attribute) =>
            {
                Log.Verbose(" - " + roleName + " = " + attribute.Value);

                string value;
                if (rolesByCount.TryGetValue(roleName, out value))
                {
                    attribute.SetValue(value);
                }
            });

            Log.Verbose("Remote instance counts: ");
            WithInstanceCounts(remoteConfigurationFile, (roleName, attribute) =>
            {
                rolesByCount[roleName] = attribute.Value;
                Log.Verbose(" - " + roleName + " = " + attribute.Value);
            });

            Log.Verbose("Replacing local instance count settings with remote settings: ");
            WithInstanceCounts(localConfigurationFile, (roleName, attribute) =>
            {
                string value;
                if (!rolesByCount.TryGetValue(roleName, out value)) 
                    return;

                attribute.SetValue(value);
                Log.Verbose(" - " + roleName + " = " + attribute.Value);
            });
        }

        static void WithInstanceCounts(XContainer configuration, Action<string, XAttribute> roleAndCountAttributeCallback)
        {
            foreach (var roleElement in configuration.Elements()
                .SelectMany(e => e.Elements())
                .Where(e => e.Name.LocalName == "Role"))
            {
                var roleNameAttribute = roleElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "name");
                if (roleNameAttribute == null)
                    continue;

                var instancesElement = roleElement.Elements().FirstOrDefault(e => e.Name.LocalName == "Instances");
                if (instancesElement == null)
                    continue;

                var countAttribute = instancesElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "count");
                if (countAttribute == null)
                    continue;

                roleAndCountAttributeCallback(roleNameAttribute.Value, countAttribute);
            }
        }

        static void WithConfigurationSettings(XContainer configuration, Action<string, string, XAttribute> roleSettingNameAndValueAttributeCallback)
        {
            foreach (var roleElement in configuration.Elements()
                .SelectMany(e => e.Elements())
                .Where(e => e.Name.LocalName == "Role"))
            {
                var roleNameAttribute = roleElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "name");
                if (roleNameAttribute == null)
                    continue;

                var configSettingsElement = roleElement.Elements().FirstOrDefault(e => e.Name.LocalName == "ConfigurationSettings");
                if (configSettingsElement == null)
                    continue;

                foreach (var settingElement in configSettingsElement.Elements().Where(e => e.Name.LocalName == "Setting"))
                {
                    var nameAttribute = settingElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "name");
                    if (nameAttribute == null)
                        continue;

                    var valueAttribute = settingElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "value");
                    if (valueAttribute == null)
                        continue;

                    roleSettingNameAndValueAttributeCallback(roleNameAttribute.Value, nameAttribute.Value, valueAttribute);
                }
            }
        }
    }
}