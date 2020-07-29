﻿using System;
using System.IO;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    public class VariableReplacerFixture : CalamariFixture
    {
        readonly IFileFormatVariableReplacer replacer;

        public VariableReplacerFixture(IFileFormatVariableReplacer replacer)
        {
            this.replacer = replacer;
        }

        public string Replace(IVariables variables, string existingFile, Func<string, string> outputFileReader)
        {
            var temp = Path.GetTempFileName();
            File.Copy(GetFixtureResouce("Samples", existingFile), temp, true);

            using (new TemporaryFile(temp))
            {
                replacer.ModifyFile(temp, variables);
                return outputFileReader(temp);
            }
        }

        public string Replace(IVariables variables, string existingFile)
        {
            return Replace(variables, existingFile, File.ReadAllText);
        }

        public string ReplaceToHex(IVariables variables, string existingFile)
        {
            return Replace(variables, existingFile, path => File.ReadAllBytes(path).ToReadableHexDump());
        }
    }
}