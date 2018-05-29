using System;

namespace Calamari.Integration.Scripting
{
    public class Script
    {
        public string File { get; private set; }
        public string Parameters { get; private set; }

        public Script(string file) :this (file, null)
        {
        }

        public Script(string file, string parameters)
        {
            if (string.IsNullOrEmpty(file)) throw new InvalidScriptException("File can not be null or empty.");
            File = file;
            Parameters = parameters;
        }
    }
}