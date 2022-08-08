using System;

namespace Calamari.Common.Features.Scripting
{
    public class Script
    {
        public Script(string? file, string? parameters = null)
        {
            if (string.IsNullOrEmpty(file))
                throw new InvalidScriptException("File can not be null or empty.");
            File = file;
            Parameters = parameters;
        }

        public string File { get; }
        public string? Parameters { get; }
    }
}