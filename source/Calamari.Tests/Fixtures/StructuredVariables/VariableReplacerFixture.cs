using System;
using System.IO;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    public abstract class VariableReplacerFixture : CalamariFixture
    {
        readonly ICalamariFileSystem fileSystem;
        readonly Func<ICalamariFileSystem, ILog, IFileFormatVariableReplacer> replacerFactory;

        protected VariableReplacerFixture(Func<ICalamariFileSystem, ILog, IFileFormatVariableReplacer> replacerFactory)
        {
            this.replacerFactory = replacerFactory;
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        }

        string Replace(IVariables variables, string existingFile, Func<string, string> outputFileReader)
        {
            var temp = Path.GetTempFileName();
            File.Copy(GetFixtureResource("Samples", existingFile), temp, true);

            using (new TemporaryFile(temp))
            {
                replacerFactory(fileSystem, Log).ModifyFile(temp, variables);
                return outputFileReader(temp);
            }
        }

        protected string Replace(IVariables variables, string existingFile)
        {
            return Replace(variables, existingFile, File.ReadAllText);
        }

        protected string ReplaceToHex(IVariables variables, string existingFile)
        {
            return Replace(variables, existingFile, path => File.ReadAllBytes(path).ToReadableHexDump());
        }
    }
}