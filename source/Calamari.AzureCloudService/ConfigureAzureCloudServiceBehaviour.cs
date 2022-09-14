using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Hyak.Common;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace Calamari.AzureCloudService
{
    class ConfigureAzureCloudServiceBehaviour : IPreDeployBehaviour
    {
        readonly ILog log;
        readonly AzureAccount account;
        readonly ICalamariFileSystem fileSystem;

        public ConfigureAzureCloudServiceBehaviour(ILog log, AzureAccount account, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.account = account;
            this.fileSystem = fileSystem;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            // Validate we actually have a real path to the real config file since this value is potentially passed via variable or a previous convention
            var configurationFilePath = context.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile);
            if (!fileSystem.FileExists(configurationFilePath))
                throw new CommandException("Could not find the Azure Cloud Service Configuration file: " + configurationFilePath);

            var configuration = XDocument.Parse(fileSystem.ReadFile(configurationFilePath));
            await UpdateConfigurationWithCurrentInstanceCount(configuration, configurationFilePath, context.Variables);
            UpdateConfigurationSettings(configuration, context.Variables);
            SaveConfigurationFile(configuration, configurationFilePath);
        }

        async Task<XDocument> GetConfiguration(string serviceName, DeploymentSlot slot)
        {
            using var client = account.CreateComputeManagementClient(CalamariCertificateStore.GetOrAdd(account.CertificateThumbprint, account.CertificateBytes));
            try
            {
                var response = await client.Deployments.GetBySlotAsync(serviceName, slot);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Getting deployment by slot returned HTTP Status Code: {response.StatusCode}");
                }

                return string.IsNullOrEmpty(response.Configuration)
                    ? null
                    : XDocument.Parse(response.Configuration);
            }
            catch (CloudException exception)
            {
                log.VerboseFormat("Getting deployments for service '{0}', slot {1}, returned:\n{2}", serviceName,
                                  slot.ToString(), exception.Message);
                return null;
            }
        }


        void SaveConfigurationFile(XDocument document, string configurationFilePath)
        {
            fileSystem.OverwriteFile(configurationFilePath, document.ToString());
        }

        void UpdateConfigurationSettings(XContainer configurationFile, IVariables variables)
        {
            log.Verbose("Updating configuration settings...");
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
                    log.InfoFormat("Updating setting for role {0}: {1} = {2}", roleName, settingName, setting);
                    settingValueAttribute.Value = setting;
                }
            });

            if (!foundSettings)
            {
                log.Info("No settings that match provided variables were found.");
            }
        }

        async Task UpdateConfigurationWithCurrentInstanceCount(XContainer localConfigurationFile, string configurationFileName, IVariables variables)
        {
            if (!variables.GetFlag(SpecialVariables.Action.Azure.UseCurrentInstanceCount))
                return;

            var serviceName = variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            var slot = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), variables.Get(SpecialVariables.Action.Azure.Slot));

            var remoteConfigurationFile = await GetConfiguration(serviceName, slot);

            if (remoteConfigurationFile == null)
            {
                log.InfoFormat("There is no current deployment of service '{0}' in slot '{1}', so existing instance counts will not be imported.", serviceName, slot);
                return;
            }

            var rolesByCount = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            log.Verbose("Local instance counts (from " + Path.GetFileName(configurationFileName) + "): ");
            WithInstanceCounts(localConfigurationFile, (roleName, attribute) =>
            {
                log.Verbose(" - " + roleName + " = " + attribute.Value);

                string value;
                if (rolesByCount.TryGetValue(roleName, out value))
                {
                    attribute.SetValue(value);
                }
            });

            log.Verbose("Remote instance counts: ");
            WithInstanceCounts(remoteConfigurationFile, (roleName, attribute) =>
            {
                rolesByCount[roleName] = attribute.Value;
                log.Verbose(" - " + roleName + " = " + attribute.Value);
            });

            log.Verbose("Replacing local instance count settings with remote settings: ");
            WithInstanceCounts(localConfigurationFile, (roleName, attribute) =>
            {
                string value;
                if (!rolesByCount.TryGetValue(roleName, out value))
                    return;

                attribute.SetValue(value);
                log.Verbose(" - " + roleName + " = " + attribute.Value);
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