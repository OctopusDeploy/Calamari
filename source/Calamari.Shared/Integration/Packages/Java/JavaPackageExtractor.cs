using System.Collections.Generic;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Packages.Java
{
    public class JavaPackageExtractor : GenericPackageExtractor
    {
        readonly ILog log;
        JarTool JarTool { get; }
        public JavaPackageExtractor(ILog log, JarTool jarTool) : base(log)
        {
            this.log = log;
            JarTool = jarTool;
        }

        protected override IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new JarExtractor(JarTool)
        }; 
    }
}