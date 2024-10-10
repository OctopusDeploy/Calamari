using System;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AzureServiceFabric.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.LogParser;
using Calamari.Testing.Requirements;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureServiceFabric.Tests
{
    [TestFixture]
    [WindowsTest]
    public class DeployAzureServiceFabricAppCommandFixture
    {
        string clientCertThumbprint;
        string clientCertStoreLocation;
        string clientCertStoreName;
        string clientCertPfx;
        string clientCertSubjectCommonName;
        string connectionEndpoint;
        string serverCertThumbprint;
        
        string applicationName;
        
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            clientCertThumbprint = await ExternalVariables.Get(ExternalVariable.ServiceFabricClientCertThumbprint, cancellationToken);
            clientCertStoreLocation = await ExternalVariables.Get(ExternalVariable.ServiceFabricClientCertStoreLocation, cancellationToken);
            clientCertStoreName = await ExternalVariables.Get(ExternalVariable.ServiceFabricClientCertStoreName, cancellationToken);
            clientCertPfx = await ExternalVariables.Get(ExternalVariable.ServiceFabricClientCertPfx, cancellationToken);
            clientCertSubjectCommonName = await ExternalVariables.Get(ExternalVariable.ServiceFabricClientCertSubjectCommonName, cancellationToken);
            connectionEndpoint = await ExternalVariables.Get(ExternalVariable.ServiceFabricConnectionEndpoint, cancellationToken);
            serverCertThumbprint = await ExternalVariables.Get(ExternalVariable.ServiceFabricServerCertThumbprint, cancellationToken);
        }

        [SetUp]
        public void SetUp()
        {
            applicationName = $"fabric:/CalamariTest-{Guid.NewGuid():N}";
        }

        [TearDown]
        public async Task TearDown()
        {
            var xc = ServiceFabricHelper.GetX509Credentials(clientCertThumbprint,
                                                            clientCertStoreLocation,
                                                            clientCertStoreName,
                                                            serverCertThumbprint,
                                                            clientCertSubjectCommonName);
            try
            {
                var fabricClient = new FabricClient(xc, connectionEndpoint);

                await fabricClient.ApplicationManager.DeleteApplicationAsync(new DeleteApplicationDescription(new Uri(applicationName)));
            }
            catch (Exception ex)
            {
                // SF throw weird exception messages if you don't have the certificate installed.
                if (ex.InnerException != null && ex.InnerException.Message.Contains("0x80071C57"))
                    throw new Exception($"Service Fabric was unable to to find certificate with thumbprint '{clientCertThumbprint}' in Cert:\\{clientCertStoreLocation}\\{clientCertStoreName}. Please make sure you have installed the certificate on the Octopus Server before attempting to use/reference it in a Service Fabric Cluster target.");
                throw;
            }
        }

        [Test]
        public async Task Deploy_MarksServiceFabricAppOfAwesomeness_To_StaticCluster()
        {
            var (packagePath, packageName, packageVersion) = PrepareServiceFabricAppZipPackage();

            var deployment = await CommandTestBuilder.CreateAsync<DeployAzureServiceFabricAppCommand, Program>()
                                                     .WithArrange(context =>
                                                                  {
                                                                      context.WithPackage(packagePath, packageName, packageVersion);

                                                                      AddVariables(context);
                                                                  })
                                                     .Execute();

            deployment.Outcome.Should().Be(TestExecutionOutcome.Successful);
        }

        void AddVariables(CommandTestBuilderContext context)
        {
            context.Variables.Add(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint,connectionEndpoint);
            context.Variables.Add(SpecialVariables.Action.ServiceFabric.SecurityMode, AzureServiceFabricSecurityMode.SecureClientCertificate.ToString());
            context.Variables.Add(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint, serverCertThumbprint);
            context.Variables.Add(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, clientCertStoreLocation);
            context.Variables.Add(SpecialVariables.Action.ServiceFabric.CertificateStoreName, clientCertStoreName);

            const string clientCertVariable = "Certificates-1";
            context.Variables.Add(SpecialVariables.Action.ServiceFabric.ClientCertVariable, clientCertVariable);

            context.Variables.Add($"{clientCertVariable}.{CertificateVariables.Properties.Pfx}", clientCertPfx);
            context.Variables.Add($"{clientCertVariable}.{CertificateVariables.Properties.Thumbprint}", clientCertThumbprint);
            context.Variables.Add($"{clientCertVariable}.{CertificateVariables.Properties.Password}", string.Empty);
            context.Variables.Add($"{clientCertVariable}.SubjectCommonName", clientCertSubjectCommonName);

            context.Variables.Add("ApplicationName", applicationName);
        }

        static (string packagePath, string packageName, string packageVersion) PrepareServiceFabricAppZipPackage()
        {
            (string packagePath, string packageName, string packageVersion) packageInfo;

            var testAssemblyLocation = new FileInfo(Assembly.GetExecutingAssembly().Location);
            var sourceZip = Path.Combine(testAssemblyLocation.Directory.FullName, "Packages", "MarksServiceFabricAppOfAwesomeness.1.0.0.zip");

            packageInfo.packagePath = sourceZip;
            packageInfo.packageVersion = "1.0.0";
            packageInfo.packageName = "MarksServiceFabricAppOfAwesomeness";

            return packageInfo;
        }
    }
}