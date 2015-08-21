using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Calamari.Integration.ServiceMessages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Integration.Scripting.WindowsPowerShell
{
    [TestFixture]
    public class PowershellBootstrapperFixture
    {

        [Test]
        public void Powershell()
        {

            var output = Path.ChangeExtension(Path.GetTempFileName(), "ps1");
            File.WriteAllText(output, "Write-Host $mysecrect");
            var cd = new CalamariVariableDictionary();
            cd.Set("foo", "bar");
            cd.SetSensitive("mysecrect","KingKong");
            
            var psse = new PowerShellScriptEngine();
            var capture = new CaptureCommandOutput();

            var runner = new CommandLineRunner(capture);
            psse.Execute(output, cd, runner);


            Assert.IsTrue(capture.Infos.Any(line => line.Contains("KingKong")));
        }

        [Test]
        public void CSharp()
        {

            var output = Path.ChangeExtension(Path.GetTempFileName(), "cs");
            File.WriteAllText(output, "Write-Host $mysecrect");
            var cd = new CalamariVariableDictionary();
            cd.Set("foo", "bar");
            cd.SetSensitive("mysecrect", "KingKong");

            var psse = new PowerShellScriptEngine();
            var capture = new CaptureCommandOutput();

            var runner = new CommandLineRunner(capture);
            psse.Execute(output, cd, runner);


            Assert.IsTrue(capture.Infos.Any(line => line.Contains("KingKong")));
        }


    }
}
