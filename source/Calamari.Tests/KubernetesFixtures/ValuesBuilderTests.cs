using System;
using System.Collections.Generic;
using Calamari.Kubernetes.Conventions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ValuesBuilderTests
    {

        [Test]
        public void AllTheThings()
        {
            var values = new Dictionary<string, object>
            {
                {"foo.bar1", "Hel lo"},
                {"size", 21},
                {"foo.bat.mat", true},
                {"foo.bar2", "World"},
            };
            var yaml = RawValuesToYamlConverter.Convert(values);

            var expect = @"foo:
  bar1: Hel lo
  bar2: World
  bat:
    mat: true
size: 21
".Replace("\r\n", "\n").Replace("\n", Environment.NewLine);;
            Assert.AreEqual(expect, yaml);
        }
    }
}