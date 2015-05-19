using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Azure;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfigureAzureCloudServiceConventionFixture
    {
        ICalamariFileSystem fileSystem;
        ISubscriptionCloudCredentialsFactory credentialsFactory;
        IAzureCloudServiceConfigurationRetriever configurationRetriever;
        RunningDeployment deployment;
        VariableDictionary variables;
        ConfigureAzureCloudServiceConvention convention;
        const string stagingDirectory = "C:\\Applications\\Foo"; 
        const string defaultConfigurationFile = "ServiceConfiguration.Cloud.cscfg";
        const string azureSubscriptionId = "8affaa7d-3d74-427c-93c5-2d7f6a16e754";
        const string certificateThumbprint = "86B5C8E5553981FED961769B2DA3028C619596AC";
        const string certificateBytes = "ThisIsNotAValidCertificate";
        const string cloudServiceName = "AcmeOnline";
        const DeploymentSlot deploymentSlot = DeploymentSlot.Production; 

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            credentialsFactory = Substitute.For<ISubscriptionCloudCredentialsFactory>();
            configurationRetriever = Substitute.For<IAzureCloudServiceConfigurationRetriever>(); 
            variables = new VariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, stagingDirectory);
            deployment = new RunningDeployment(stagingDirectory, variables);

            variables.Set(SpecialVariables.Action.Azure.SubscriptionId, azureSubscriptionId);
            variables.Set(SpecialVariables.Action.Azure.CertificateThumbprint, certificateThumbprint);
            variables.Set(SpecialVariables.Action.Azure.CertificateBytes, certificateBytes);
            variables.Set(SpecialVariables.Action.Azure.CloudServiceName, cloudServiceName);
            variables.Set(SpecialVariables.Action.Azure.Slot, deploymentSlot.ToString());

            credentialsFactory.GetCredentials(azureSubscriptionId, certificateThumbprint, certificateBytes)
                .Returns(new FakeSubscriptionCloudCredentials(azureSubscriptionId));

            convention = new ConfigureAzureCloudServiceConvention(fileSystem, credentialsFactory, configurationRetriever);
        }

        [Test]
        public void ShouldUseUserSpecifiedConfigurationFile()
        {
            const string userSpecifiedFile = "MyCustomCloud.cscfg";
            variables.Set(ConfigureAzureCloudServiceConvention.ConfigurationFileNameVariable, userSpecifiedFile);
            string result = null;
            ArrangeOriginalConfigurationFile(Path.Combine(stagingDirectory, userSpecifiedFile), SimpleConfigSample, x=>result=x);

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
        }

        [Test]
        public void IfNoUserSpecifiedConfigThenShouldUseEnvironmentConfigurationFile()
        {
            variables.Set(SpecialVariables.Environment.Name, "Production");
            const string environmentConfigurationFile = "ServiceConfiguration.Production.cscfg";
            variables.Set(ConfigureAzureCloudServiceConvention.ConfigurationFileNameVariable, environmentConfigurationFile);
            string result = null;
            ArrangeOriginalConfigurationFile(Path.Combine(stagingDirectory, environmentConfigurationFile), SimpleConfigSample, x => result=x);

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
        }

        [Test]
        public void IfNoUserSpecifiedOrEnvironmentFileThenShouldUseDefault()
        {
            string result = null;
            ArrangeOriginalConfigurationFile(Path.Combine(stagingDirectory, defaultConfigurationFile), SimpleConfigSample, x => result=x);

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
        }

        [Test]
        public void ShouldUseExistingInstanceCountIfSpecified()
        {
            string result = null;
            variables.Set(SpecialVariables.Action.Azure.UseCurrentInstanceCount, true.ToString());
            ArrangeOriginalConfigurationFile(Path.Combine(stagingDirectory, defaultConfigurationFile), SimpleConfigSample, x => result=x);
            configurationRetriever.GetConfiguration(
                Arg.Is<SubscriptionCloudCredentials>(x => x.SubscriptionId == azureSubscriptionId), cloudServiceName, deploymentSlot)
                .Returns(XDocument.Parse(RemoteConfigWithInstanceCount4));

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
            AssertRoleHasInstanceCount(result, "Humpty.Web", 4);
            AssertRoleHasInstanceCount(result, "Humpty.Worker", 6);
        }

        [Test]
        public void ShouldReplaceSettingsValuesWithVariables()
        {
            variables.Set("HelloMessage", "Bonjour");
            string result = null;
            ArrangeOriginalConfigurationFile(Path.Combine(stagingDirectory, defaultConfigurationFile), SimpleConfigSample, x => result=x);

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
            AssertRoleHasSettingWithValue(result, "Humpty.Web", "HelloMessage", "Bonjour");
            AssertRoleHasSettingWithValue(result, "Humpty.Worker", "HelloMessage", "Bonjour");
        }

        void ArrangeOriginalConfigurationFile(string configurationFile, string content, Action<string> captureResultingConfiguration)
        {
            fileSystem.FileExists(configurationFile).Returns(true);
            fileSystem.ReadFile(configurationFile).Returns(content);

            fileSystem.OverwriteFile(configurationFile, Arg.Do<string>(captureResultingConfiguration));
        }

        void AssertIsValidConfigurationFile(string configuration)
        {
            var xml = XDocument.Parse(configuration);
            Assert.AreEqual("ServiceConfiguration", xml.Root.Name.LocalName);
        }

        void AssertRoleHasInstanceCount(string configuration, string role, int expectedInstanceCount)
        {
            var xml = XDocument.Parse(configuration);
            var nameSpace = xml.Root.GetDefaultNamespace();
            var roleElement = xml.Root.Elements(nameSpace + "Role").Single(x => x.Attribute("name").Value == role);
            var actualInstanceCount = int.Parse(roleElement.Element(nameSpace + "Instances").Attribute("count").Value);

            Assert.AreEqual(expectedInstanceCount, actualInstanceCount);
        }

        void AssertRoleHasSettingWithValue(string configuration, string role, string settingName, string settingValue)
        {
            var xml = XDocument.Parse(configuration);
            var nameSpace = xml.Root.GetDefaultNamespace();
            var roleElement = xml.Root.Elements(nameSpace + "Role").Single(x => x.Attribute("name").Value == role);
            var configurationSettingsElement = roleElement.Element(nameSpace + "ConfigurationSettings");
            Assert.AreEqual(settingValue, 
                configurationSettingsElement.Elements(nameSpace + "Setting").Single(x => x.Attribute("name").Value == settingName).Attribute("value").Value);
        }


        const string SimpleConfigSample = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ServiceConfiguration serviceName=""Humpty"" xmlns=""http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration"" osFamily=""2"" osVersion=""*"" schemaVersion=""2012-10.1.8"">
  <Role name=""Humpty.Web"">
    <Instances count=""1"" />
    <ConfigurationSettings>
      <Setting name=""Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"" value=""UseDevelopmentStorage=true"" />
      <Setting name=""HelloMessage"" value=""Hello world! This is a web role!"" />
    </ConfigurationSettings>
  </Role>
  <Role name=""Humpty.Worker"">
    <Instances count=""1"" />
    <ConfigurationSettings>
      <Setting name=""Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"" value=""UseDevelopmentStorage=true"" />
      <Setting name=""HelloMessage"" value=""Hello world! This is a worker!"" />
    </ConfigurationSettings>
  </Role>
</ServiceConfiguration>
";

        const string RemoteConfigWithInstanceCount4 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ServiceConfiguration serviceName=""Humpty"" xmlns=""http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration"" osFamily=""2"" osVersion=""*"" schemaVersion=""2012-10.1.8"">
  <Role name=""Humpty.Web"">
    <Instances count=""4"" />
  </Role>
  <Role name=""Humpty.Worker"">
    <Instances count=""6"" />
  </Role>
</ServiceConfiguration>
";
    }
}