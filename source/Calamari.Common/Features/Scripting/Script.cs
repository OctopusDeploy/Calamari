using System;

namespace Calamari.Common.Features.Scripting
{
    public class Script
    {
        public Script(string file)
            : this(file, null)
        {
        }

        public Script(string file, string parameters)
        {
            if (string.IsNullOrEmpty(file))
                throw new InvalidScriptException("File can not be null or empty.");
            File = file;
            Parameters = parameters;
        }

        public string File { get; }
        public string Parameters { get; }
    }
}