using System;
using System.IO;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using NUnit.Framework;

namespace Calamari.Azure.Tests.ResourceGroups
{
    [TestFixture]
    public class ResourceGroupTemplateNormalizerFixture
    {
        ResourceGroupTemplateNormalizer subject;

        [SetUp]
        public void SetUp()
        {
           subject = new ResourceGroupTemplateNormalizer(); 
        }

        [Test]
        public void ShouldNormallizeParametersWithEnvelope()
        {
            var result = subject.Normalize(ReadParametersFile("params_with_envelope.json"));

            Assert.AreEqual(StripWhiteSpace(GetParameter()), StripWhiteSpace(result));
        }

        [Test]
        public void ShouldNormallizeParametersSansEnvelope()
        {
            var result = subject.Normalize(ReadParametersFile("params_sans_envelope.json"));

            Assert.AreEqual(StripWhiteSpace(GetParameter()), StripWhiteSpace(result));
        }

        private string ReadParametersFile(string fileName)
        {
            var path = GetType().Namespace.Replace("Calamari.Azure.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return File.ReadAllText(Path.Combine(AzureTestEnvironment.TestWorkingDirectory, path, fileName));            
        }

        private string StripWhiteSpace(string input)
        {
            return input.Replace(" ", string.Empty)
                        .Replace(Environment.NewLine, string.Empty);
        }

        private string GetParameter()
        {
            return @"{
                        'lifeTheUniverseAndEverything': {
                            'value': '42'
                        },
                        'password': {
                            'reference': {
                                'keyVault': {
                                    'id': 'id/othervalue/test'
                                },
                                'secretName': 'secretName'
                            }
                        }
                    }".Replace("'", "\"");
        }
    }
}