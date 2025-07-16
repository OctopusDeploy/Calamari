using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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
    public class HealthCheckCommandFixture
    {       
        string clientCertThumbprint;
        string clientCertStoreLocation;
        string clientCertStoreName;
        string clientCertPfx;
        string clientCertSubjectCommonName;
        string connectionEndpoint;
        string serverCertThumbprint;
        
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
        
        [Test]
        public async Task Execute_HealthCheck_Against_Static_Cluster()
        {
            var deployment = await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                                     .WithArrange(AddVariables)
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
        }
    }
}