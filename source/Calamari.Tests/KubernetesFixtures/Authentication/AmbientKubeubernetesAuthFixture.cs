﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Authentication
{
    public class LocalK8sCluster: IDisposable
    {
        readonly string name;

        public LocalK8sCluster(string name)
        {
            this.name = name;
        }

        public void Create()
        {
            EnsureClusterDoesNotExit();
            //kind create cluster --name kind-shoemaker
            Runner.RunCommandToSuccess("kind", "create", "cluster",
                                       "--name",
                                       $"{name.ToLower()}");
        }

        void EnsureClusterDoesNotExit()
        {
            var (log, res) = Runner.RunCommandToSuccess("kind", "get", "clusters");
            if (log.Messages.Any(m => m.FormattedMessage.Equals(name)))
            {
                DeleteCluster();
            }
        }

        void DeleteCluster()
        {
            Runner.RunCommandToSuccess("kind", "delete", "cluster",
                                       "--name",
                                       $"{name.ToLower()}");
        }
        public void Dispose()
        {
            DeleteCluster();
        }
    }

    /*[TestFixture]
    [Ignore("Still Need to get this working")]
    public class Foobar
    {
        //dotnet test --no-build --filter "FullyQualifiedName=Calamari.Tests.KubernetesFixtures.Authentication.Foobar.DoThings"cd
        [Test]
        public void DoThings()
        {
            Console.WriteLine("OK");
        }
    }*/

    [TestFixture]
    [Ignore("Still Need to get this working")]
    public class AmbientKubernetesAuthFixture
    {
        [Test]
        //[Ignore("Still Need to get this working")]
        public void Foobar()
        {

            using (var c = new LocalK8sCluster("foobar"))
            {
                c.Create();
                RunJon();
            }
        }


        const string job = "foobased";

        public void RunJon()
        {
            Runner.RunCommand("kubectl", new[] { "delete", "job", job });

            var loc = Assembly.GetAssembly(this.GetType());
            var dir = Path.GetDirectoryName(loc.Location);


            Runner.RunCommandToSuccess("kubectl",
                                       "create",
                                       "job",
                                       job,
                                       "--image=mcr.microsoft.com/dotnet/sdk:7.0",
                                       "--",
                                       "dotnet",
                                       "\"--version\"");
            Runner.RunCommandToSuccess("kubectl", "wait", "--for=condition=complete", $"job/{job}", "--timeout=120s");
            var (logs, result) = Runner.RunCommandToSuccess("kubectl", "logs", $"job/{job}");
            Runner.RunCommandToSuccess("kubectl", new[] { "delete", "job", job });
        }

        /*
        public void GetJobDetails()
        {

            kubectl delete job my-job || true
            kubectl apply -f ./jobs/my-job.yaml
            kubectl wait --for=condition=complete job/my-job --timeout=60s
                echo "Job output:"
            kubectl logs job/my-job



            var (log, result) = RunCommand("kubectl", new[] { "describe", "job", "testthing" });
            result.VerifySuccess();

            kubectl delete pod foobarxx
            kubectl run foobarxx --image mcr.microsoft.com/dotnet/sdk:7.0 --restart Never  -- dotnet "--version"
            kubectl logs foobarxx


            kubectl delete job foobar || true
            kubectl create job foobar --image=mcr.microsoft.com/dotnet/sdk:7.0 -- dotnet "--version"
            kubectl wait --for=condition=complete job/foobar --timeout=60s
            kubectl logs job/foobar

            kubectl describe job foobar
            kubectl get pods --selector=batch.kubernetes.io/job-name=foobar --output=jsonpath='{.items[*].metadata.name}'
            kubectl logs foobar-rhqnc
            kubectl delete job foobar
        }*/


    }

    public static class Runner
    {
        public static (InMemoryLog Log, CommandResult Result) RunCommandToSuccess(string executable, params string[] arguments)
        {
            var result = RunCommand(executable, arguments);
            result.VerifySuccess();
            return (result.Log, result.Result);
        }

        public static (InMemoryLog Log, CommandResult Result, Action VerifySuccess ) RunCommand(string executable, params string[] arguments)
        {
            var log = new InMemoryLog();
            var runner = new CommandLineRunner(log, new CalamariVariables());

            var result = runner.Execute(new CommandLineInvocation(executable, arguments));
            return (log, result, () => result.VerifySuccess());
        }
    }
}