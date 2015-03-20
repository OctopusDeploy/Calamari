using System.IO;
using System.Text.RegularExpressions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Substitutions
{
    [TestFixture]
    public class SubstitutionsFixture : CalamariFixture
    {
        [Test]
        public void ShouldSubstitute()
        {
            var variables = new VariableDictionary();
            variables["ServerEndpoints[FOREXUAT01].Name"] = "forexuat01.local";
            variables["ServerEndpoints[FOREXUAT01].Port"] = "1566";
            variables["ServerEndpoints[FOREXUAT02].Name"] = "forexuat02.local";
            variables["ServerEndpoints[FOREXUAT02].Port"] = "1566";

            var result = PerformTest("Samples\\Servers.json", variables);

            Assert.That(Regex.Replace(result, "\\s+", ""), Is.EqualTo(@"{""Servers"":[{""Name"":""forexuat01.local"",""Port"":1566},{""Name"":""forexuat02.local"",""Port"":1566}]}"));
        }

        string PerformTest(string sampleFile, VariableDictionary variables)
        {
            var temp = Path.GetTempFileName();
            using (new TemporaryFile(temp))
            {    
                var substituter = new FileSubstituter();
                substituter.PerformSubstitution(MapSamplePath(sampleFile), variables, temp);
                return File.ReadAllText(temp);
            }
        }
    }
}
