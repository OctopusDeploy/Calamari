using System;
using System.IO;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    public abstract class DeployPackageFixture : CalamariFixture
    {
        protected ICalamariFileSystem FileSystem { get; private set; }
        protected IVariables Variables { get; private set; }
        protected string StagingDirectory { get; private set; }
        protected string CustomDirectory { get; private set; }

        public virtual void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            // Ensure staging directory exists and is empty 
            StagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            CustomDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestCustom");
            FileSystem.EnsureDirectoryExists(StagingDirectory);
            FileSystem.PurgeDirectory(StagingDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(StagingDirectory, "DeploymentJournal.xml"));

            Variables = new VariablesFactory(FileSystem).Create(new CommonOptions("test"));
            Variables.Set(TentacleVariables.Agent.ApplicationDirectoryPath, StagingDirectory);
            Variables.Set("PreDeployGreeting", "Bonjour");
        }

        public virtual void CleanUp()
        {
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(StagingDirectory, FailureOptions.IgnoreFailure);
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(CustomDirectory, FailureOptions.IgnoreFailure);
        }

        protected CalamariResult DeployPackage(string packageName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                Variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                    .Action("deploy-package")
                    .Argument("package", packageName)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}