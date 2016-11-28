using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Extensibility;
using Calamari.Extensibility.FileSystem;
using Calamari.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Octostache;
using FailureOptions = Calamari.Extensibility.FileSystem.FailureOptions;

namespace Calamari.Tests.Fixtures.Deployment
{
    public abstract class DeployPackageFixture : CalamariFixture
    {
        protected ICalamariFileSystem FileSystem { get; private set; }
        protected IVariableDictionary Variables { get; private set; }
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

            Variables = new CalamariVariableDictionary();
            Variables.EnrichWithEnvironmentVariables();
            Variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, StagingDirectory);
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
                Variables.Set(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath, packageName);
                Variables.Save(variablesFile.FilePath);

                /*
                    return InProcessInvoke(InProcessCalamari()
                        .Action("run-feature")
                        .Argument("feature", "DeployPackage")
                        .Argument("variables", variablesFile.FilePath));
*/
                      return InProcessInvoke(InProcessCalamari()
                        .Action("deploy-package")
                        .Argument("package", packageName)
                        .Argument("variables", variablesFile.FilePath));

            }
        }
    }
}