using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Testing.Tools;
using Octostache;

namespace Calamari.Testing
{
    public class CommandTestBuilderContext
    {
        public List<(string? filename, Stream contents)> Files = new List<(string?, Stream)>();
        
        public List<IDeploymentTool> Tools { get; } = new();

        internal bool withStagedPackageArgument;

        public VariableDictionary Variables { get; } = new VariableDictionary();

        public IList<string> Args { get; } = new List<string>();

        public CommandTestBuilderContext WithStagedPackageArgument()
        {
            withStagedPackageArgument = true;
            return this;
        }

        public CommandTestBuilderContext AddVariable(string key, string value)
        {
            Variables.Add(key, value);
            return this;
        }

        public CommandTestBuilderContext WithDataFile(string fileContents, string? fileName = null)
        {
            WithDataFile(fileContents.EncodeInUtf8Bom(), fileName);
            return this;
        }

        public CommandTestBuilderContext WithDataFileNoBom(string fileContents, string? fileName = null)
        {
            WithDataFile(fileContents.EncodeInUtf8NoBom(), fileName);
            return this;
        }

        public CommandTestBuilderContext WithDataFile(byte[] fileContents, string? fileName = null)
        {
            WithDataFile(new MemoryStream(fileContents), fileName);
            return this;
        }

        public CommandTestBuilderContext WithDataFile(Stream fileContents, string? fileName = null, Action<int>? progress = null)
        {
            Files.Add((fileName, fileContents));
            return this;
        }
        
        public CommandTestBuilderContext WithTool(IDeploymentTool tool)
        {
            Tools.Add(tool);
            return this;
        }

        public CommandTestBuilderContext WithArg(string arg)
        {
            Args.Add(arg);
            return this;
        }
    }
}