using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.StructuredVariables;
using NUnit.Framework;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class YamlEventStreamClassifierFixture
    {
        [Test]
        public void YamlEventPathMonitorCalculatesValuePaths()
        {
            var input = @"
                key: val
                key2: val2
                mapA: 
                  keyA: vAA
                  keyB: vAB
                seqC: 
                  - C0
                  -
                    F: vC1F
                    G: vC1G
                  -
                    - vC20
                    - vC21
                  - C3
                key3: val3";
            var expectedScalarValues = new List<(string path, string value)>
            {
                ("key", "val"),
                ("key2", "val2"),
                ("mapA:keyA", "vAA"),
                ("mapA:keyB", "vAB"),
                ("seqC:0", "C0"),
                ("seqC:1:F", "vC1F"),
                ("seqC:1:G", "vC1G"),
                ("seqC:2:0", "vC20"),
                ("seqC:2:1", "vC21"),
                ("seqC:3", "C3"),
                ("key3", "val3")
            };
            CollectionAssert.AreEqual(expectedScalarValues, GetScalarValues(input));
        }

        List<(string path, string value)> GetScalarValues(string input)
        {
            var result = new List<(string path, string value)>();

            using (var textReader = new StringReader(input))
            {
                var parser = new Parser(textReader);
                var classifier = new YamlEventStreamClassifier();
                while (parser.MoveNext())
                {
                    var found = classifier.Process(parser.Current);
                    if (found is YamlNode<Scalar> scalarValue)
                        result.Add((scalarValue.Path, scalarValue.Event.Value));
                }
            }

            return result;
        }
    }
}