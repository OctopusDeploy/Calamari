using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Shared;

namespace Calamari.Extensibility
{
    public interface IFileSubstitution
    {
        void PerformSubstitution(string sourceFile, IVariableDictionary variables);
    }
}
