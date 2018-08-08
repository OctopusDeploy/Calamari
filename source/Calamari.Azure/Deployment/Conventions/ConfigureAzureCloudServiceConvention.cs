using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Calamari.Azure.Integration;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class ConfigureAzureCloudServiceConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ISubscriptionCloudCredentialsFactory credentialsFactory;
        readonly IAzureCloudServiceConfigurationRetriever configurationRetriever;

        public ConfigureAzureCloudServiceConvention(ICalamariFileSystem fileSystem, ISubscriptionCloudCredentialsFactory subscriptionCloudCredentialsFactory, IAzureCloudServiceConfigurationRetriever configurationRetriever)
        {
            this.fileSystem = fileSystem;
            this.credentialsFactory = subscriptionCloudCredentialsFactory;
            this.configurationRetriever = configurationRetriever;
        }

        public void Install(RunningDeployment deployment)
        {
            // Validate we actually have a real path to the real config file since this value is potentially passed via variable or a previous convention
            var configurationFilePath = deployment.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile);
            if (!fileSystem.FileExists(configurationFilePath))
                throw new CommandException("Could not find the Azure Cloud Service Configuration file: " + configurationFilePath);

            var configuration = XDocument.Parse(fileSystem.ReadFile(configurationFilePath));
            UpdateConfigurationWithCurrentInstanceCount(configuration, configurationFilePath, deployment.Variables);
            UpdateConfigurationSettings(configuration, deployment.Variables);
            SaveConfigurationFile(configuration, configurationFilePath);
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
                            variables.Get(settingName) ??
                            (variables.GetNames().Contains(settingName) ? "" : null);
                
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
            if (!variables.GetFlag(SpecialVariables.Action.Azure.UseCurrentInstanceCount))
                return;

            var serviceName = variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            var slot = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), variables.Get(SpecialVariables.Action.Azure.Slot));

            var remoteConfigurationFile = configurationRetriever.GetConfiguration(
                credentialsFactory.GetCredentials(variables),
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