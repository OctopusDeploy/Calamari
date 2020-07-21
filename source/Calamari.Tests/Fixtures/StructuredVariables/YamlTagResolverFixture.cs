using System;
using System.IO;
using System.Linq;
using Calamari.Common.Features.StructuredVariables;
using NUnit.Framework;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class YamlTagResolverFixture
    {
        string ResolveUri(string input)
        {
            using (var textReader = new StringReader(input))
            {
                var parser = new Parser(textReader);
                while (parser.MoveNext())
                    if (parser.Current is Scalar scalar)
                        return YamlTagResolver.ResolveTag(scalar);

                return null;
            }
        }

        [Test]
        public void ResolvesTags()
        {
            Assert.AreEqual(YamlTagResolver.TagUriNull, new[] { "!!null ~", "null", "~" }.Select(ResolveUri).Distinct().Single());
            Assert.AreEqual(YamlTagResolver.TagUriBool, new[] { "!!bool false", "false", "no" }.Select(ResolveUri).Distinct().Single());
            Assert.AreEqual(YamlTagResolver.TagUriInt, new[] { "!!int 33", "33", "-1" }.Select(ResolveUri).Distinct().Single());
            Assert.AreEqual(YamlTagResolver.TagUriFloat, new[] { "!!float 1.5", "1.5", "-0.01" }.Select(ResolveUri).Distinct().Single());
            Assert.AreEqual(YamlTagResolver.TagUriStr, new[] { "!!str bananas", "foo", "bar" }.Select(ResolveUri).Distinct().Single());
        }
    }
}