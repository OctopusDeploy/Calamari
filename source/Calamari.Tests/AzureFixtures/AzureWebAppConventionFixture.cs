#if AZURE

using System;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Calamari.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AzureFixtures
{
    [TestFixture]
    public class AzureWebAppConventionFixture
    {
        [Test]
        public void UsingAManagementCertificateCausesAWarning()
        {
            using (var logs = new ProxyLog())
            {
                var convention = new AzureWebAppConvention();

                var vars = new CalamariVariables();
                vars.Set(SpecialVariables.Account.AccountType, "AzureSubscription");

                var deployment = new RunningDeployment("", vars);

                // ignore the incomplete setup, we just want to know about whether the warning is logged
                try
                {
                    convention.Install(deployment);
                }
                catch(Exception){}

                logs.StdOut.Contains("Use of Management Certificates to deploy Azure Web App services has been deprecated").Should().BeTrue();
            }
        }
        
        [Test]
        public void UsingAServicePrincipalDoesNotCauseAWarning()
        {
            using (var logs = new ProxyLog())
            {
                var convention = new AzureWebAppConvention();

                var vars = new CalamariVariables();
                vars.Set(SpecialVariables.Account.AccountType, "AzureServicePrincipal");

                var deployment = new RunningDeployment("", vars);

                // ignore the incomplete setup, we just want to know about whether the warning is logged
                try
                {
                    convention.Install(deployment);
                }
                catch(Exception){}

                logs.StdOut.Contains("Use of Management Certificates to deploy Azure Web App services has been deprecated").Should().BeFalse();
            }
        }
    }
}

#endif