using System;
using Calamari.Common.Features.Substitutions;

namespace Calamari.Common.Features.Behaviours
{
    public class NonSensitiveSubstituteInFilesBehaviour : SubstituteInFilesBehaviour
    {
        public NonSensitiveSubstituteInFilesBehaviour(
            INonSensitiveSubstituteInFiles substituteInFiles,
            string subdirectory = "",
            ISubstituteFileMatcher? customFileMatcher = null)
            : base(substituteInFiles, subdirectory, customFileMatcher)
        {
        }
    }
}