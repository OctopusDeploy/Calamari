using System;
using System.Collections.Immutable;
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
using Bicep.Core.TypeSystem.Az;
using Bicep.Core.Workspaces;

namespace Calamari.AzureResourceGroup
{
    public interface IBicepTemplateCompiler
    {
        string Compile(string template);
    }
    
    public class BicepTemplateCompiler : IBicepTemplateCompiler
    {
        private readonly IFileResolver fileResolver;
        private readonly IModuleDispatcher moduleDispatcher;
        private readonly IConfigurationManager configurationManager;
        private readonly IApiVersionProvider apiVersionProvider;
        private readonly IBicepAnalyzer linterAnalyzer;

        public BicepTemplateCompiler(IFileResolver fileResolver, IModuleDispatcher moduleDispatcher, IConfigurationManager configurationManager, IApiVersionProvider apiVersionProvider, IBicepAnalyzer linterAnalyzer)
        {
            this.fileResolver = fileResolver;
            this.moduleDispatcher = moduleDispatcher;
            this.configurationManager = configurationManager;
            this.apiVersionProvider = apiVersionProvider;
            this.linterAnalyzer = linterAnalyzer;
        }
        
        public string Compile(string inputPath)
        {
            var uri = PathHelper.FilePathToFileUrl(Path.GetFullPath(inputPath));
            var grouping = SourceFileGroupingBuilder.Build(fileResolver, moduleDispatcher, new Workspace(), uri);
            var features = new FeatureProvider();
            var compilation = new Compilation(
                features, 
                new DefaultNamespaceProvider(new AzResourceTypeLoader()), 
                grouping, 
                configurationManager, 
                apiVersionProvider, 
                linterAnalyzer);
            using var compiled = new MemoryStream();
            using var writer = new StreamWriter(compiled);
            
            var model = compilation.GetEntrypointSemanticModel();
            var emitter = new TemplateEmitter(model, new EmitterSettings(features));
            emitter.Emit(writer);
            compiled.Flush();

            compiled.Position = 0;
            using var reader = new StreamReader(compiled);
            return reader.ReadToEnd();
        }
    }
}