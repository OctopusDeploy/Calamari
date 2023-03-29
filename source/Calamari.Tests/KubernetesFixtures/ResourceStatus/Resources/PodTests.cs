using System.Linq;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using KubernetesResources = Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class PodTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithContainerStatuses(new ContainerStatus[]
                {
                    new ContainerStatus()
                    {
                        State = new State { Running = new Running() },
                        Ready = true
                    },
                    new ContainerStatus()
                    {
                        State = new State { Waiting = new Waiting { Reason = "CrashLoopBackOff" } },
                        Ready = false,
                        RestartCount = 3
                    }
                })
                .Build();
                
            var pod = ResourceFactory.FromJson(podResponse);
            
            pod.Should().BeEquivalentTo(new
            {
                Ready = "1/2",
                Restarts = 3,
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed
            });
        }
        
        [Test]
        public void WhenThePodCannotBeScheduled_TheStatusShouldBePending()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Pending");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.InProgress);
        }
        
        [Test]
        public void WhenInitContainerIsBeingCreatedWithoutErrors_TheStatusShouldShowNumberOfInitContainersReady()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithInitContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "PodInitializing" } },
                    new State { Terminated = new Terminated { Reason = "Completed" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Init:1/2");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.InProgress);
        }
        
        [Test]
        public void WhenInitContainerCouldNotPullImages_TheStatusShouldShowInitImagePullBackOff()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithInitContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "ImagePullBackOff" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Init:ImagePullBackOff");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
        
        [Test]
        public void WhenInitContainerCouldNotExecuteSuccessfully_TheStatusShouldShowCrashLoopBackOff()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithInitContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "CrashLoopBackOff" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Init:CrashLoopBackOff");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
        
        [Test]
        public void WhenContainerIsBeingCreated_TheStatusShouldShowContainerCreating()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "ContainerCreating" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("ContainerCreating");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.InProgress);
        }
        
        [Test]
        public void WhenContainerCannotPullImage_TheStatusShouldShowImagePullBackOff()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "ImagePullBackOff" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("ImagePullBackOff");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
        
        [Test]
        public void WhenContainerFailsExecutionAndRestarting_TheStatusShouldShowCrashLoopBackOff()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "CrashLoopBackOff" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("CrashLoopBackOff");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
        
        [Test]
        public void WhenContainerCannotStartExecutionAndWillNotRestart_TheStatusShouldShowContainerCannotRun()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Failed")
                .WithContainerStates(new State[]
                {
                    new State { Terminated = new Terminated { Reason = "ContainerCannotRun" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("ContainerCannotRun");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
        
        [Test]
        public void WhenContainerFailsExecutionAndWillNotRestart_TheStatusShouldShowError()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Failed")
                .WithContainerStates(new State[]
                {
                    new State { Terminated = new Terminated { Reason = "Error" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Error");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
        
        [Test]
        public void WhenContainerHasCompletedSuccessfully_TheStatusShouldShowCompleted()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Succeeded")
                .WithContainerStates(new State[]
                {
                    new State { Terminated = new Terminated { Reason = "Completed" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Completed");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Successful);
        }
        
        [Test]
        public void WhenContainerIsRunningWithoutErrors_TheStatusShouldShowRunning()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Running")
                .WithContainerStates(new State[]
                {
                    new State { Running = new Running() }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("Running");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Successful);
        }

        [Test]
        public void WhenMoreThanOneContainersFailed_TheStatusShouldShowTheFirstError()
        {
            var podResponse = new PodResponseBuilder()
                .WithPhase("Pending")
                .WithContainerStates(new State[]
                {
                    new State { Waiting = new Waiting { Reason = "ImagePullBackOff" } },
                    new State { Waiting = new Waiting { Reason = "CrashLoopBackOff" } }
                })
                .Build();
    
            var pod = (Pod)ResourceFactory.FromJson(podResponse);

            pod.Status.Should().Be("ImagePullBackOff");
            pod.ResourceStatus.Should().Be(KubernetesResources.ResourceStatus.Failed);
        }
    }
    
    public class PodResponseBuilder
    {
        private const string Template = @"
{{
    ""kind"": ""Pod"",
    ""metadata"": {{
        ""name"": ""Test"",
        ""uid"": ""123""
    }},
    ""status"": {{
        ""phase"": ""{0}"",
        ""initContainerStatuses"": {1},
        ""containerStatuses"": {2}
    }}
}}";
    
        private string Phase { get; set; } = "Running";
        private string InitContainerStatuses { get; set; } = "[]";
        private string ContainerStatuses { get; set; } = "[]";
        
        public string Build()
        {
            return string.Format(Template, Phase, InitContainerStatuses, ContainerStatuses);
        }
        
        public PodResponseBuilder WithPhase(string phase)
        {
            Phase = phase;
            return this;
        }
    
        public PodResponseBuilder WithInitContainerStates(params State[] initContainerStates)
        {
            var statuses = initContainerStates.Select(state => new ContainerStatus { State = state });
            InitContainerStatuses = JsonConvert.SerializeObject(statuses);
            return this;
        }
    
        public PodResponseBuilder WithContainerStates(params State[] containerStates)
        {
            var statuses = containerStates.Select(state => new ContainerStatus { State = state });
            ContainerStatuses = JsonConvert.SerializeObject(statuses);
            return this;
        }

        public PodResponseBuilder WithContainerStatuses(params ContainerStatus[] containerStatuses)
        {
            ContainerStatuses = JsonConvert.SerializeObject(containerStatuses);
            return this;
        }
    }
}