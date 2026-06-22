using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("Calamari.AzureResourceGroup.Tests")]
// Allows NSubstitute (Castle DynamicProxy) to mock internal types such as IResourceGroupTemplateNormalizer in unit tests.
[assembly:InternalsVisibleTo("DynamicProxyGenAssembly2")]