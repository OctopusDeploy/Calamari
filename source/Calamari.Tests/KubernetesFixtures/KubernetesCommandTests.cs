#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Calamari.Tests.KubernetesFixtures.Tools;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    internal static class VariablesExtensionMethods
    {
        public static void SetInlineScriptVariables(this IVariables variables, string bashScript, string powershellScript)
        {
            variables.Set(Deployment.SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), bashScript);
            variables.Set(Deployment.SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell), powershellScript);
        }
        
        public static void SetAuthenticationDetails(this IVariables variables)
        {
            variables.Set(SpecialVariables.ClientCertificate, "UserCert");
            variables.Set(SpecialVariables.CertificatePem("UserCert"),  System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(KubernetesTestsGlobalContext.Instance.ClusterUser.ClientCertPem)));
            variables.Set(SpecialVariables.PrivateKeyPem("UserCert"),  System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(KubernetesTestsGlobalContext.Instance.ClusterUser.ClientCertKey)));
        }

        public static void SetClusterDetails(this IVariables variables)
        {
            variables.Set(SpecialVariables.ClusterUrl, KubernetesTestsGlobalContext.Instance.ClusterEndpoint.ClusterUrl);
            variables.Set(SpecialVariables.CertificateAuthority, "ClientCert");
            variables.Set(SpecialVariables.CertificatePem("ClientCert"),  System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(KubernetesTestsGlobalContext.Instance.ClusterEndpoint.ClusterCert)));
        }
        
        public static void SetVariablesForKubernetesResourceStatusCheck(this IVariables variables, int timeout = 30, string deploymentWait = "wait")
        {
            variables.Set("Octopus.Action.Kubernetes.ResourceStatusCheck", "True");
            variables.Set(SpecialVariables.DeploymentWait, deploymentWait);
            variables.Set("Octopus.Action.Kubernetes.DeploymentTimeout", timeout.ToString());
            variables.Set("Octopus.Action.Kubernetes.PrintVerboseKubectlOutputOnError", "True");
        }
    }

    [TestFixture]
    public class KubernetesCommandTests : CalamariFixture
    {
        [Test]
        public void GivenInvalidYaml_ShouldFail()
        {
            var variables = new CalamariVariables();
            variables.SetClusterDetails();
            variables.SetAuthenticationDetails();
            variables.SetVariablesForKubernetesResourceStatusCheck();
            var ns = CreateString();
            variables.Set(SpecialVariables.Namespace, ns);
            
            const string failToDeploymentResource = @"apiVersion: v1
kind: Pod
metadata:
  name: nginx
spec:
  invalid-spec: this-is-not-valid
  containers:
  - name: nginx
    image: nginx";

            var output = ExecuteRawYamlCommand(variables, failToDeploymentResource);

            output.AssertFailure();
            output.AssertOutputContains("Pod in version \"v1\" cannot be handled as a Pod: strict decoding error: unknown field \"spec.invalid-spec\"");
            //$"error: error parsing {fileName}: error converting YAML to JSON: yaml: line 7: could not find expected ':'";
        }
        
        [Test]
        public void GivenKubectlScript_ShouldExecute()
        {
            var variables = new CalamariVariables();
            variables.SetClusterDetails();
            variables.SetAuthenticationDetails();
            variables.Set(SpecialVariables.Namespace, "default");

            var sampleMessage = CreateString();
            var cmd = $"echo \"{sampleMessage}\"{Environment.NewLine}kubectl cluster-info";
            variables.SetInlineScriptVariables(cmd,cmd);
            var output = ExecuteCommand(variables, RunScriptCommand.Name, null);

            output.AssertSuccess();
            output.AssertOutputContains(sampleMessage);
            output.AssertOutputContains($"Kubernetes control plane is running at {KubernetesTestsGlobalContext.Instance.ClusterEndpoint.ClusterUrl}");
        }
        
        CalamariResult ExecuteRawYamlCommand(CalamariVariables variables, string yaml)
        {
            variables.Set(SpecialVariables.CustomResourceYamlFileName, "**/*.{yml,yaml}");
            using var workingDirectory = TemporaryDirectory.Create();
            CreateResourceYamlFile(workingDirectory.DirectoryPath, DeploymentFileName, yaml);
            variables.Set(SpecialVariables.CustomResourceYamlFileName, DeploymentFileName);
            variables.Set(KnownVariables.OriginalPackageDirectoryPath, workingDirectory.DirectoryPath);
                
            var output = ExecuteCommand(variables, KubernetesApplyRawYamlCommand.Name, workingDirectory.DirectoryPath);
            return output;
        }

        private static string CreateResourceYamlFile(string directory, string fileName, string content)
        {
            var pathToCustomResource = Path.Combine(directory, fileName);
            File.WriteAllText(pathToCustomResource, content);
            return pathToCustomResource;
        }
        private Func<string,string> CreateAddCustomResourceFileFunc(IVariables variables, string yamlContent)
        {
            return directory =>
                   {
                       CreateResourceYamlFile(directory, DeploymentFileName, yamlContent);
                       if (!variables.IsSet(SpecialVariables.CustomResourceYamlFileName))
                       {
                           variables.Set(SpecialVariables.CustomResourceYamlFileName, DeploymentFileName);
                       }
                       return null;
                   };
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
        
     
        private const string DeploymentFileName = "customresource.yml";
        
    }
    
}
#endif