using System;

namespace Calamari.Common.Features.Scripting
{
    public class Script
    {
#pragma warning disable CS8618 // Protected by guard clause
        public Script(string? file, string? parameters = null)
        {
            if (string.IsNullOrEmpty(file))
                throw new InvalidScriptException("File can not be null or empty.");
            File = file;
            Parameters = parameters;
        }
#pragma warning restore CS8618

        public string File { get; }
        public string? Parameters { get; }
    }
}