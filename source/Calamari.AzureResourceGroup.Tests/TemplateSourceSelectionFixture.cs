using Calamari.Common.Plumbing.Variables;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests;

[TestFixture]
public class TemplateSourceSelectionFixture
{
    [Test]
    public void InlineSource_ReadsTemplateVariablesWithConventionalFileDefaults()
    {
        var variables = new CalamariVariables
        {
            { SpecialVariables.Action.Azure.TemplateSource, "Inline" }
        };

        var (templateFile, parametersFile, filesInPackageOrRepository) = variables.SelectTemplateInputs();

        Assert.Multiple(() =>
        {
            Assert.That(filesInPackageOrRepository, Is.False);
            Assert.That(templateFile, Is.EqualTo("template.json"));
            Assert.That(parametersFile, Is.EqualTo("parameters.json"));
        });
    }

    // Package and GitRepository are deliberately treated as the same file-based source by production,
    // so both must select the mandatory ResourceGroupTemplate variables. This replaces the redundant
    // cloud tests that previously deployed to Azure just to exercise this branch.
    [TestCase("Package")]
    [TestCase("GitRepository")]
    public void FileBasedSource_ReadsMandatoryResourceGroupTemplateVariables(string templateSource)
    {
        var variables = new CalamariVariables
        {
            { SpecialVariables.Action.Azure.TemplateSource, templateSource },
            { SpecialVariables.Action.Azure.ResourceGroupTemplate, "main.json" },
            { SpecialVariables.Action.Azure.ResourceGroupTemplateParameters, "params.json" }
        };

        var (templateFile, parametersFile, filesInPackageOrRepository) = variables.SelectTemplateInputs();

        Assert.Multiple(() =>
        {
            Assert.That(filesInPackageOrRepository, Is.True);
            Assert.That(templateFile, Is.EqualTo("main.json"));
            Assert.That(parametersFile, Is.EqualTo("params.json"));
        });
    }
}
