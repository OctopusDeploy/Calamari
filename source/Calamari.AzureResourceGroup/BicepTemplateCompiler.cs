using System;
using System.IO;
using Bicep.Core.Analyzers.Interfaces;
using Bicep.Core.Analyzers.Linter.ApiVersions;
using Bicep.Core.Configuration;
using Bicep.Core.Emit;
using Bicep.Core.Features;
using Bicep.Core.FileSystem;
using Bicep.Core.Registry;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.Workspaces;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.AzureResourceGroup
{
    public interface IBicepTemplateCompiler
    {
        string Compile(string template);
    }
    
    public class BicepTemplateCompiler : IBicepTemplateCompiler
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly IFileResolver fileResolver;
        private readonly IModuleDispatcher moduleDispatcher;
        private readonly IConfigurationManager configurationManager;
        private readonly IApiVersionProvider apiVersionProvider;
        private readonly IBicepAnalyzer linterAnalyzer;
        private readonly IFeatureProvider featureProvider;
        private readonly INamespaceProvider namespaceProvider;

        public BicepTemplateCompiler(
            ICalamariFileSystem fileSystem,
            IFileResolver fileResolver, 
            IModuleDispatcher moduleDispatcher, 
            IConfigurationManager configurationManager, 
            IApiVersionProvider apiVersionProvider, 
            IBicepAnalyzer linterAnalyzer,
            IFeatureProvider featureProvider,
            INamespaceProvider namespaceProvider)
        {
            this.fileSystem = fileSystem;
            this.fileResolver = fileResolver;
            this.moduleDispatcher = moduleDispatcher;
            this.configurationManager = configurationManager;
            this.apiVersionProvider = apiVersionProvider;
            this.linterAnalyzer = linterAnalyzer;
            this.featureProvider = featureProvider;
            this.namespaceProvider = namespaceProvider;
        }
        
        public string Compile(string template)
        {
            // we don't use the stream created here,
            // since the `Emit` method exposed by Bicep only accept a Uri for input rather than a Stream
            _ = fileSystem.CreateTemporaryFile("", out var tempFile);
            fileSystem.WriteAllText(tempFile, template);
            
            var uri = PathHelper.FilePathToFileUrl(tempFile);

            string compiled;
            try
            {
                compiled = Compile(uri);
            }
            finally
            {
                fileSystem.DeleteFile(tempFile);
            }
            return compiled;
        }

        private string Compile(Uri uri)
        {
            var grouping = SourceFileGroupingBuilder.Build(fileResolver, moduleDispatcher, new Workspace(), uri);
            var compilation = new Compilation(
                featureProvider, 
                namespaceProvider, 
                grouping, 
                configurationManager, 
                apiVersionProvider, 
                linterAnalyzer);
            
            using var compiled = new MemoryStream();
            using var writer = new StreamWriter(compiled);
            
            new TemplateEmitter(
                compilation.GetEntrypointSemanticModel(), 
                new EmitterSettings(featureProvider))
                .Emit(writer);
            
            compiled.Flush();
            compiled.Position = 0;
            using var reader = new StreamReader(compiled);
            return reader.ReadToEnd();
        }
    }
}