using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class JobTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            var jobResponse = new JobResponseBuilder()
                .WithCompletions(3)
                .WithBackoffLimit(4)
                .WithSucceeded(3)
                .WithStartTime("2023-03-29T00:00:00Z")
                .WithCompletionTime("2023-03-30T02:03:04Z")
                .Build();
            
            var job = ResourceFactory.FromJson(jobResponse, new Options());
            
            job.Should().BeEquivalentTo(new
            {
                Kind = "Job",
                Name = "my-job",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Completions = "3/3",
                Duration = "1.02:03:04",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }

        [Test]
        public void WhenWaitForJobsNotEnabled_ShouldHaveStatusOfSuccess()
        {
            var jobResponse = new JobResponseBuilder()
                .WithCompletions(3)
                .WithBackoffLimit(4)
                .WithFailed(4)
                .Build();

            var job = ResourceFactory.FromJson(jobResponse, new Options());

            job.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }
        
        [Test]
        public void WhenWaitForJobsIsEnabled_ShouldHaveStatusOfFailedIfBackOffLimitHasBeenReached()
        {
            var jobResponse = new JobResponseBuilder()
                .WithCompletions(3)
                .WithBackoffLimit(4)
                .WithFailed(4)
                .Build();

            var job = ResourceFactory.FromJson(jobResponse, new Options() { WaitForJobs = true });

            job.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed);
        }

        [Test]
        public void WhenWaitForJobsIsEnabled_ShouldHaveStatusOfSuccessfulIfDesiredCompletionsHaveBeenAchieved()
        {
            var jobResponse = new JobResponseBuilder()
                .WithCompletions(3)
                .WithSucceeded(3)
                .WithFailed(1)
                .Build();

            var job = ResourceFactory.FromJson(jobResponse, new Options() { WaitForJobs = true });

            job.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        [Test]
        public void WhenWaitForJobsIsEnabled_ShouldHaveStatusOfInProgressIsDesiredCompletionsHaveNotBeenReached()
        {
            var jobResponse = new JobResponseBuilder()
                .WithCompletions(3)
                .WithSucceeded(2)
                .WithFailed(1)
                .Build();

            var job = ResourceFactory.FromJson(jobResponse, new Options() { WaitForJobs = true });

            job.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }
    }

    public class JobResponseBuilder
    {
        private const string Template = @"{{
    ""kind"": ""Job"",
    ""metadata"": {{
        ""name"": ""my-job"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    }},
    ""spec"": {{
        ""completions"": {0},
        ""backoffLimit"": {1}
    }},
    ""status"": {{
        ""succeeded"": {2},
        ""failed"": {3},
        ""startTime"": ""{4}"",
        ""completionTime"": ""{5}""
    }}
}}";

        private int Completions { get; set; }
        private int BackoffLimit { get; set; }
        private int Succeeded { get; set; }
        private int Failed { get; set; }
        private string StartTime { get; set; }
        private string CompletionTime { get; set; }

        public JobResponseBuilder WithCompletions(int completions)
        {
            Completions = completions;
            return this;
        }

        public JobResponseBuilder WithBackoffLimit(int backoffLimit)
        {
            BackoffLimit = backoffLimit;
            return this;
        }

        public JobResponseBuilder WithSucceeded(int succeeded)
        {
            Succeeded = succeeded;
            return this;
        }

        public JobResponseBuilder WithFailed(int failed)
        {
            Failed = failed;
            return this;
        }
        
        public JobResponseBuilder WithStartTime(string startTime)
        {
            StartTime = startTime;
            return this;
        }

        public JobResponseBuilder WithCompletionTime(string completionTime)
        {
            CompletionTime = completionTime;
            return this;
        }

        public string Build()
        {
            return string.Format(Template, Completions, BackoffLimit, Succeeded, Failed, StartTime, CompletionTime);
        }        
        
    }
}

