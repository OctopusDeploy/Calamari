using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.NamingIsHard.Tests
{
    [TestFixture]
    public class MyActionHandlerValidatorFixture
    {
        MyActionHandlerValidator? validator;

        [SetUp]
        public void Setup()
        {
            validator = new MyActionHandlerValidator();
        }

        [Test]
        public void Validate_Defaults_No_Error()
        {
            var context = new DeploymentActionValidationContext(SpecialVariables.MyActionHandlerTypeName,
                                                                new Dictionary<string, string>
                                                                {
                                                                    { SpecialVariables.Action.MyProp, "None empty string" }
                                                                },
                                                                new List<PackageReference>());

            var result = validator.TestValidate(context);

            result.IsValid.Should().BeTrue();
        }
    }
}