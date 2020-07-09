using System;
using System.IO;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture, Explicit]
    public class YamlTransformApproachComparisonDemoFixture
    {
        [Test]
        public void YamlTransformApproachComparisonDemo()
        {
            var input = @"Numbers: !!omap [ one: 1, two: 2, three: 3, octy: 010, norway: no ]";

            Demo(nameof(YamlDotNetSerializerIdentityTransform), () => YamlDotNetSerializerIdentityTransform(input));
            Demo(nameof(YamlDotNetParserEmitterIdentityTransform),
                () => YamlDotNetParserEmitterIdentityTransform(input));
            //Demo(nameof(SharpYamlSerializerIdentityTransform), () => SharpYamlSerializerIdentityTransform(input));
            //Demo(nameof(SharpYamlParserEmitterIdentityTransform), () => SharpYamlParserEmitterIdentityTransform(input));
        }

        public void Demo(string name, Func<string> transform)
        {
            string output = "";
            try
            {
                output = transform();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"** {name} failed: {ex}");
            }

            Console.WriteLine($"{name}:");
            Console.WriteLine("[[");
            Console.WriteLine(output);
            Console.WriteLine("]]");
        }

        string YamlDotNetSerializerIdentityTransform(string input)
        {
            using (var textReader = new StringReader(input))
            using (var textWriter = new StringWriter())
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
                var data = deserializer.Deserialize(textReader);
                new YamlDotNet.Serialization.Serializer().Serialize(textWriter, data ?? "");

                textWriter.Close();
                return textWriter.ToString();
            }
        }

        string YamlDotNetParserEmitterIdentityTransform(string input)
        {
            using (var textReader = new StringReader(input))
            using (var textWriter = new StringWriter())
            {
                var parser = new YamlDotNet.Core.Parser(textReader);
                var emitter = new YamlDotNet.Core.Emitter(textWriter);
                while (parser.MoveNext())
                {
                    if (parser.Current != null)
                        emitter.Emit(parser.Current);
                }

                textWriter.Close();
                return textWriter.ToString();
            }
        }

        /*string SharpYamlSerializerIdentityTransform(string input)
        {
            using (var textReader = new StringReader(input))
            using (var textWriter = new StringWriter())
            {
                var serializer = new SharpYaml.Serialization.Serializer(new SharpYaml.Serialization.SerializerSettings
                {
                    EmitShortTypeName = true
                });
                var data = serializer.Deserialize(textReader);
                serializer.Serialize(textWriter, data);

                textWriter.Close();
                return textWriter.ToString();
            }
        }*/

        /*string SharpYamlParserEmitterIdentityTransform(string input)
        {
            using (var textReader = new StringReader(input))
            using (var textWriter = new StringWriter())
            {
                var parser = new SharpYaml.Parser(textReader);
                var emitter = new SharpYaml.Emitter(textWriter);
                while (parser.MoveNext())
                {
                    emitter.Emit(parser.Current);
                }

                textWriter.Close();
                return textWriter.ToString();
            }
        }*/
    }
}