using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Integration;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Azure.Tests.Conventions
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class ConfigureAzureCloudServiceConventionFixture
    {
        ICalamariFileSystem fileSystem;
        ISubscriptionCloudCredentialsFactory credentialsFactory;
        IAzureCloudServiceConfigurationRetriever configurationRetriever;
        RunningDeployment deployment;
        VariableDictionary variables;
        ConfigureAzureCloudServiceConvention convention;
        const string StagingDirectory = "C:\\Applications\\Foo"; 
        const string DefaultConfigurationFileName = "ServiceConfiguration.Cloud.cscfg";
        const string AzureSubscriptionId = "8affaa7d-3d74-427c-93c5-2d7f6a16e754";
        const string CertificateThumbprint = "86B5C8E5553981FED961769B2DA3028C619596AC";
        const string CertificateBytes = "ThisIsNotAValidCertificate";
        const string CloudServiceName = "AcmeOnline";
        const DeploymentSlot DeploymentSlot = Microsoft.WindowsAzure.Management.Compute.Models.DeploymentSlot.Production;

        string result;

        [SetUp]
        public void SetUp()
        {
            result = null;

            fileSystem = Substitute.For<ICalamariFileSystem>();
            credentialsFactory = Substitute.For<ISubscriptionCloudCredentialsFactory>();
            configurationRetriever = Substitute.For<IAzureCloudServiceConfigurationRetriever>(); 
            variables = new VariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, StagingDirectory);
            deployment = new RunningDeployment(StagingDirectory, variables);

            variables.Set(SpecialVariables.Action.Azure.SubscriptionId, AzureSubscriptionId);
            variables.Set(SpecialVariables.Action.Azure.CertificateThumbprint, CertificateThumbprint);
            variables.Set(SpecialVariables.Action.Azure.CertificateBytes, CertificateBytes);
            variables.Set(SpecialVariables.Action.Azure.CloudServiceName, CloudServiceName);
            variables.Set(SpecialVariables.Action.Azure.Slot, DeploymentSlot.ToString());

            credentialsFactory.GetCredentials(AzureSubscriptionId, CertificateThumbprint, CertificateBytes)
                .Returns(new FakeSubscriptionCloudCredentials(AzureSubscriptionId));

            convention = new ConfigureAzureCloudServiceConvention(fileSystem, credentialsFactory, configurationRetriever);
        }

        [Test]
        [TestCase("A file that doesn't exist")]
        [TestCase("")]
        [TestCase(null)]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Could not find the Azure Cloud Service Configuration file", MatchType = MessageMatch.StartsWith)]
        public void ShouldThrowSensibleExceptionIfOriginalConfigurationFileIsMissingOrInvalid(string configurationFilePath)
        {
            variables.Set(SpecialVariables.Action.Azure.Output.ConfigurationFile, configurationFilePath);

            convention.Install(deployment);
        }

        [Test]
        public void ShouldUseExistingInstanceCountIfSpecified()
        {
            ArrangeOriginalConfigurationFileForSuccess(Path.Combine(StagingDirectory, DefaultConfigurationFileName), SimpleConfigSample, x => result = x);

            variables.Set(SpecialVariables.Action.Azure.UseCurrentInstanceCount, true.ToString());
            configurationRetriever.GetConfiguration(
                Arg.Is<SubscriptionCloudCredentials>(x => x.SubscriptionId == AzureSubscriptionId), CloudServiceName, DeploymentSlot)
                .Returns(XDocument.Parse(RemoteConfigWithInstanceCount4));

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
            AssertRoleHasInstanceCount(result, "Humpty.Web", 4);
            AssertRoleHasInstanceCount(result, "Humpty.Worker", 6);
        }

        [Test]
        public void ShouldReplaceSettingsValuesWithVariables()
        {
            ArrangeOriginalConfigurationFileForSuccess(Path.Combine(StagingDirectory, DefaultConfigurationFileName), SimpleConfigSample, x => result = x);
            
            variables.Set("HelloMessage", "Bonjour");

            convention.Install(deployment);

            AssertIsValidConfigurationFile(result);
            AssertRoleHasSettingWithValue(result, "Humpty.Web", "HelloMessage", "Bonjour");
            AssertRoleHasSettingWithValue(result, "Humpty.Worker", "HelloMessage", "Bonjour");
        }

        void ArrangeOriginalConfigurationFileForSuccess(string configurationFilePath, string content, Action<string> captureResultingConfiguration)
        {
            variables.Set(SpecialVariables.Action.Azure.Output.ConfigurationFile, configurationFilePath);
            fileSystem.FileExists(configurationFilePath).Returns(true);
            fileSystem.ReadFile(configurationFilePath).Returns(content);

            fileSystem.OverwriteFile(configurationFilePath, Arg.Do<string>(captureResultingConfiguration));
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