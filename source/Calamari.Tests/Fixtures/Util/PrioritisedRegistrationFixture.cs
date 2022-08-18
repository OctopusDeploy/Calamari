using Autofac;
using Calamari.Common.Plumbing.Extensions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class PrioritisedRegistrationFixture
    {
        ContainerBuilder builder;
        
        [SetUp]
        public void SetUp()
        {
            builder = new ContainerBuilder();
            builder.RegisterPrioritisedList<ITestService>();
        }
        
        [Test]
        public void WithNoServices_ReturnsEmptyList()
        {
            var services = ResolvePrioritisedList();
            
            services.Should().BeEmpty();
        }

        [Test]
        public void WithUnprioritisedServices_ReturnsListInAnyOrder()
        {
            builder.RegisterType<TestServiceA>().As<ITestService>();
            builder.RegisterType<TestServiceB>().As<ITestService>();

            var services = ResolvePrioritisedList();

            services.Should().Contain(its => its.Identifier == "ServiceA");
            services.Should().Contain(its => its.Identifier == "ServiceB");
        }
        
        [Test]
        public void WithPrioritisedServices_ReturnsListInPriorityOrder()
        {
            builder.RegisterType<TestServiceB>().As<ITestService>().WithPriority(1);
            builder.RegisterType<TestServiceA>().As<ITestService>().WithPriority(2);

            var services = ResolvePrioritisedList();

            services[0].Identifier.Should().Be("ServiceB");
            services[1].Identifier.Should().Be("ServiceA");
        }
        
        [Test]
        public void WithPrioritisedServices_ReturnsListInPriorityOrder_RegardlessOfRegistrationOrder()
        {
            builder.RegisterType<TestServiceA>().As<ITestService>().WithPriority(2);
            builder.RegisterType<TestServiceB>().As<ITestService>().WithPriority(1);

            var services = ResolvePrioritisedList();

            services[0].Identifier.Should().Be("ServiceB");
            services[1].Identifier.Should().Be("ServiceA");
        }
        
        [Test]
        public void WithMixedPrioritisedAndUnprioritisedServices_ReturnsPrioritisedFirst_FollowedByUnprioritised()
        {
            builder.RegisterType<TestServiceA>().As<ITestService>().WithPriority(2);
            builder.RegisterType<TestServiceB>().As<ITestService>();
            builder.RegisterType<TestServiceC>().As<ITestService>().WithPriority(1);

            var services = ResolvePrioritisedList();

            services[0].Identifier.Should().Be("ServiceC");
            services[1].Identifier.Should().Be("ServiceA");
            services[2].Identifier.Should().Be("ServiceB");
        }
        
        [Test]
        public void WithDuplicatePrioritisations_DoesntBlowUp()
        {
            builder.RegisterType<TestServiceA>().As<ITestService>().WithPriority(1);
            builder.RegisterType<TestServiceB>().As<ITestService>().WithPriority(1);
            builder.RegisterType<TestServiceC>().As<ITestService>().WithPriority(1);

            var services = ResolvePrioritisedList();

            services.Count.Should().Be(3);
        }

        PrioritisedList<ITestService> ResolvePrioritisedList()
        {
            var container = builder.Build();

            using (var scope = container.BeginLifetimeScope())
            {
                return scope.Resolve<PrioritisedList<ITestService>>();
            }
        }

        public class TestServiceA : ITestService
        {
            public string Identifier => "ServiceA";
        }
        
        public class TestServiceB : ITestService
        {
            public string Identifier => "ServiceB";
        }
        
        public class TestServiceC : ITestService
        {
            public string Identifier => "ServiceC";
        }

        public interface ITestService
        {
            string Identifier { get; }
        }
    }
}
