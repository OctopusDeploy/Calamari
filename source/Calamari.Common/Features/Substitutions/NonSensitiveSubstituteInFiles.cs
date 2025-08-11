using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public class NonSensitiveSubstituteInFiles : SubstituteInFiles, INonSensitiveSubstituteInFiles
    {
        public NonSensitiveSubstituteInFiles(ILog log, ICalamariFileSystem fileSystem, INonSensitiveFileSubstituter fileSubstituter, INonSensitiveVariables variables)
            : base(log, fileSystem, fileSubstituter, variables)
        {
        }
    }
}