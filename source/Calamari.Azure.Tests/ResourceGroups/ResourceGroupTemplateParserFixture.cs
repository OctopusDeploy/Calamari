using System;
using System.IO;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using NUnit.Framework;

namespace Calamari.Azure.Tests.ResourceGroups
{
    [TestFixture]
    public class ResourceGroupTemplateParserFixture
    {
        ResourceGroupTemplateParameterParser subject;

        [SetUp]
        public void SetUp()
        {
           subject = new ResourceGroupTemplateParameterParser(); 
        }

        [Test]
        public void CanParseParametersWithEnvelope()
        {
            var result = subject.ParseParameters(ReadParametersFile("params_with_envelope.json"));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("42", result["lifeTheUniverseAndEverything"].Value);
        }

        [Test]
        public void CanParseParametersSansEnvelope()
        {
            var result = subject.ParseParameters(ReadParametersFile("params_sans_envelope.json"));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("42", result["lifeTheUniverseAndEverything"].Value);
        }

        private string ReadParametersFile(string fileName)
        {
            var path = GetType().Namespace.Replace("Calamari.Azure.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return File.ReadAllText(Path.Combine(AzureTestEnvironment.TestWorkingDirectory, path, fileName));
            
        }
    }
}