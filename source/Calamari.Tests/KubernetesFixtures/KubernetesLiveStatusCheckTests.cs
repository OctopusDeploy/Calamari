#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesLiveStatusCheckTests : CalamariFixture
    {
        [Test]
        public void GivenUndeployableResource_ShouldFail()
        {
            var variables = new CalamariVariables();
            variables.SetClusterDetails();
            variables.SetAuthenticationDetails();
            variables.SetVariablesForKubernetesResourceStatusCheck( 5);
            var ns = CreateString();
            variables.Set(SpecialVariables.Namespace, ns);
            
            var undeployableYaml = @"apiVersion: v1
kind: Pod
metadata:
  name: nginx
spec:
  containers:
  - name: nginx
    image: nginx-bad-container-name:1.14.2";

            var output = ExecuteRawYamlCommand(variables, undeployableYaml);

            output.AssertFailure();
            output.AssertOutputContains($"Resource Status Check: the following resources are still in progress by the end of the timeout:{Environment.NewLine}"
                                        + $" - Pod/nginx in namespace {ns}");
            output.AssertOutputContains("Resource status check terminated because the timeout has been reached but some resources are still in progress");
        }

        [Test]
        public void GivenValidResource_ShouldSucceed()
        {
            var variables = new CalamariVariables();
            variables.SetClusterDetails();
            variables.SetAuthenticationDetails();
            variables.SetVariablesForKubernetesResourceStatusCheck();
            var ns = CreateString();
            variables.Set(SpecialVariables.Namespace, ns);
            var deployableYaml = @"apiVersion: v1
kind: Pod
metadata:
  name: nginx
spec:
  containers:
  - name: nginx
    image: nginx:1.14.2";

            var output = ExecuteRawYamlCommand(variables, deployableYaml);

            output.AssertSuccess();
            output.AssertOutputContains($"Resource Status Check: 1 new resources have been added:{Environment.NewLine}"
                                        + $" - Pod/nginx in namespace {ns}");
        }

        [Test]
        public void GivenJob_WhenNoWait_ShouldCompleteWithoutWaitingForJob()
        {
            var yaml = @"apiVersion: batch/v1
kind: Job
metadata:
  name: sleep
spec:
  template:
    spec:
      containers:
      - name: alpine
        image: alpine
        command: [""/bin/sh"",""-c""]
        args: [""sleep 10; echo fail; exit 1""]
        command: [""sleep"",  ""10""]
      restartPolicy: Never";
            var variables = new CalamariVariables();
            variables.SetClusterDetails();
            variables.SetAuthenticationDetails();
            variables.SetVariablesForKubernetesResourceStatusCheck(timeout: 5);
            var ns = CreateString();
            variables.Set(SpecialVariables.WaitForJobs, "false");
            variables.Set(SpecialVariables.Namespace, ns);
            

            var output = ExecuteRawYamlCommand(variables, yaml);
            output.AssertSuccess();
            
            //output.AssertServiceMessage("k8s-status");
        }

        [Test]
        public void GivenJob_WhenWaitAndJobCompletesAfterTaskTimeout_ShouldTimeout()
        {
            //var jobSleep = 10;
            var yaml = @"apiVersion: batch/v1
kind: Job
metadata:
  name: sleep
spec:
  template:
    spec:
      containers:
      - name: alpine
        image: alpine
        command: [""/bin/sh"",""-c""]
        args: [""sleep 10; echo fail; exit 1""]
      restartPolicy: Never";
            var variables = new CalamariVariables();
            variables.SetClusterDetails();
            variables.SetAuthenticationDetails();
            variables.SetVariablesForKubernetesResourceStatusCheck( timeout: 5);
            variables.Set(SpecialVariables.WaitForJobs, "true");
            
            var ns = CreateString();
            variables.Set(SpecialVariables.Namespace, ns);
            

            var output = ExecuteRawYamlCommand(variables, yaml);
            output.AssertFailure();
            output.AssertOutputContains("Resource status check terminated because the timeout has been reached but some resources are still in progress");
            /*[60] = {string} "Resource Status Check: the following resources are still in progress by the end of the timeout:"
                [61] = {string} " - Job/sleep in namespace testff41f51700"
                [62] = {string} "Resource status check terminated because the timeout has been reached but some resources are still in progress"*/
        }

        CalamariResult ExecuteRawYamlCommand(CalamariVariables variables, string yaml)
        {
            const string deploymentFileName = "customresource.yml";
            variables.Set(SpecialVariables.CustomResourceYamlFileName, "**/*.{yml,yaml}");
            using var workingDirectory = TemporaryDirectory.Create();
            CreateResourceYamlFile(workingDirectory.DirectoryPath, deploymentFileName, yaml);
            variables.Set(SpecialVariables.CustomResourceYamlFileName, deploymentFileName);
            variables.Set(KnownVariables.OriginalPackageDirectoryPath, workingDirectory.DirectoryPath);
                
            var output = ExecuteCommand(variables, KubernetesApplyRawYamlCommand.Name, workingDirectory.DirectoryPath);
            return output;
        }

        static string CreateResourceYamlFile(string directory, string fileName, string content)
        {
            var pathToCustomResource = Path.Combine(directory, fileName);
            File.WriteAllText(pathToCustomResource, content);
            return pathToCustomResource;
        }
        
        string CreateString()
        {
            return $"Test{Guid.NewGuid().ToString("N").Substring(0, 10)}".ToLower();
        }

        CalamariResult ExecuteCommand(IVariables variables, string command, [CanBeNull] string workingDirectory)
        {
            using var variablesFile = new TemporaryFile(Path.GetTempFileName());
            variables.Save(variablesFile.FilePath);

            var calamariCommand = Calamari().Action(command)
                                            .Argument("variables", variablesFile.FilePath)
                                            .WithEnvironmentVariables(new Dictionary<string, string>())
                                            .OutputToLog(true);

            if (workingDirectory != null)
            {
                calamariCommand = calamariCommand.WithWorkingDirectory(workingDirectory);
            }

            return InvokeInProcess(calamariCommand, variables);
        }
        
     
        
    }
}
#endif