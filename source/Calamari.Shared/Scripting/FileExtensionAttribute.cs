using System;

namespace Calamari.Shared.Scripting
{
    public class FileExtensionAttribute : Attribute
    {
        public FileExtensionAttribute(string extension)
        {
            Extension = extension;
        }

        public string Extension { get; private set; }
    }
}