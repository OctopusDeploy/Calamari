using System;
using System.IO;

namespace Calamari.Terraform.Tests.CommonTemplates
{
    public static class TemplateLoader
    {
        private const string TemplatesFolder = "CommonTemplates";

        public static string LoadTextTemplate(string templateName)
        {
            return File.ReadAllText(Path.Combine(TemplatesFolder, templateName));
        }
    }
}
