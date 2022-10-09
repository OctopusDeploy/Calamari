using System;

namespace Calamari.Common.Features.Scripts
{
    public class FileExtensionAttribute : Attribute
    {
        public FileExtensionAttribute(string extension)
        {
            Extension = extension;
        }

        public string Extension { get; }
    }
}