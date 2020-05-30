#if AZURE

using System;
using Calamari.Azure.WebApps.Deployment.Conventions;
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
            var log = new InMemoryLog();
            var convention = new AzureWebAppService(log);

            var vars = new CalamariVariables();
            vars.Set(SpecialVariables.Account.AccountType, "AzureSubscription");

            var deployment = new RunningDeployment("", vars);

            // ignore the incomplete setup, we just want to know about whether the warning is logged
            try
            {
                convention.Install(deployment);
            }
            catch (Exception)
            {
            }

            log.StandardOut.Should().ContainMatch("Use of Management Certificates to deploy Azure Web App services has been deprecated*");
        }

        [Test]
        public void UsingAServicePrincipalDoesNotCauseAWarning()
        {
            var log = new InMemoryLog();

            var convention = new AzureWebAppService(log);

            var vars = new CalamariVariables();
            vars.Set(SpecialVariables.Account.AccountType, "AzureServicePrincipal");

            var deployment = new RunningDeployment("", vars);

            // ignore the incomplete setup, we just want to know about whether the warning is logged
            try
            {
                convention.Install(deployment);
            }
            catch (Exception)
            {
            }

            log.StandardOut.Should().NotContain("Use of Management Certificates to deploy Azure Web App services has been deprecated");
        }
    }
}

#endif