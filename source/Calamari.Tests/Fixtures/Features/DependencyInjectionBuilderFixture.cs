using System;
using Calamari.Features;
using Calamari.Features.Conventions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class DependencyInjectionBuilderFixture
    {
        private DepencencyInjectionBuilder builder;
        private CalamariContainer container;

        [SetUp]
        public void SetUp()
        {
            container = new CalamariContainer();
            builder = new DepencencyInjectionBuilder(container);
        }


        [Test]
        public void ClassWithNoConstructorCanBeInstantiated()
        {
            var item = builder.Build(typeof(ClassWithNoConstructor));
            Assert.IsInstanceOf<ClassWithNoConstructor>(item);
        }


        [Test]
        public void ConstructorWithDependenciesCanBeInstantiatedWhenDependencyRegistered()
        {
            var dateTime = new DateTime(2012, 11, 11);
            container.RegisterInstance(dateTime);
            var item = builder.Build(typeof(ClassConstructorWithDependencies));

            Assert.IsInstanceOf<ClassConstructorWithDependencies>(item);
            Assert.AreEqual(dateTime, ((ClassConstructorWithDependencies) item).Date);
        }


        [Test]
        public void ConstructorWithDependenciesFailsWhenDependencyNotRegistered()
        {
            Assert.Throws<InvalidOperationException>(
                () => builder.Build(typeof(ClassConstructorWithDependencies)),
                "Parameter `myDate` on constructor for ClassConstructorWithDependencies did not match any known or provided argument types.");
        }


        [Test]
        public void ConstructorWithMultipleDependenciesFailsWhenSpecialAttributeNotProvided()
        {
            Assert.Throws<InvalidOperationException>(
                () => builder.Build(typeof(ClassWithMultipleConstructors)),
                "Convention ClassWithMultipleConstructors has more than one constructor. If there are more than one constructors, please specify using attribute.");
        }


        private class ClassWithNoConstructor
        {
        }


        private class ClassConstructorWithDependencies
        {
            public ClassConstructorWithDependencies(DateTime myDate)
            {
                Date = myDate;
            }

            public DateTime Date { get; }
        }

        private class ClassWithMultipleConstructors
        {
            public ClassWithMultipleConstructors()
            {
            }

            public ClassWithMultipleConstructors(DateTime myDate)
            {
            }
        }
    }
}
