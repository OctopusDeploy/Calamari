using System;
using System.IO;
using System.ServiceProcess;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class DeployWindowsServiceRunningStateFixture : DeployWindowsServiceAbstractFixture
    {
        protected override string ServiceName => "RunningStateFixture";

        [Test]
        public void ShouldBeStoppedWhenStartModeIsUnchanged()
        {
            RunDeploymentAndAssertRunningState("unchanged", null, ServiceControllerStatus.Stopped);
        }
        
        [Test]
        public void ShouldBeRunningWhenStartModeIsAutoAndNoDesiredStatus()
        {
            RunDeploymentAndAssertRunningState("auto", null, ServiceControllerStatus.Running);
        }

        [Test]
        public void ShouldBeStoppedWhenStartModeIsDemandAndNoDesiredStatus()
        {
            RunDeploymentAndAssertRunningState("demand", null, ServiceControllerStatus.Stopped);
        }

        [Test]
        public void ShouldBeRunningWhenStartModeIsDemandAndDesiredStatusIsStarted()
        {
            //Setup service in stopped state
            RunDeploymentAndAssertRunningState("demand", null, ServiceControllerStatus.Stopped);
            
            RunDeploymentAndAssertRunningState("demand", "Started", ServiceControllerStatus.Running);
        }

        [Test]
        public void ShouldBeStoppedWhenStartModeIsDemandAndDesiredStatusIsStopped()
        {
            //Setup service in running state
            RunDeploymentAndAssertRunningState("demand", "Started", ServiceControllerStatus.Running);

            RunDeploymentAndAssertRunningState("demand", "Stopped", ServiceControllerStatus.Stopped);
        }
        
        [Test]
        public void ShouldBeStoppedWhenStartModeIsDemandAndDesiredStatusIsUnchangedAndServiceAlreadyStopped()
        {
            //Setup service in stopped state
            RunDeploymentAndAssertRunningState("demand", "Stopped", ServiceControllerStatus.Stopped);

            RunDeploymentAndAssertRunningState("demand", "Unchanged", ServiceControllerStatus.Stopped);
        }
        
        [Test]
        public void ShouldBeRunningWhenStartModeIsDemandAndDesiredStatusIsUnchangedAndServiceAlreadyRunning()
        {
            //Setup service in stopped state
            RunDeploymentAndAssertRunningState("demand", "Started", ServiceControllerStatus.Running);

            RunDeploymentAndAssertRunningState("demand", "Unchanged", ServiceControllerStatus.Running);
        }
    }
}