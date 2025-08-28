using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Behaviours
{
    public class NonSensitiveSubstituteInFilesBehaviour : SubstituteInFilesBehaviour
    {
        public NonSensitiveSubstituteInFilesBehaviour(
            INonSensitiveSubstituteInFiles substituteInFiles,
            string subdirectory = "")
            : base(substituteInFiles, subdirectory)
        {
        }
    }
}